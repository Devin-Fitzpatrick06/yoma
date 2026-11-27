// Assesses the live catalogue through the admin assess endpoint, then asks the API to grade the whole catalogue.
// Polite: one request at a time per course host (the API also spaces requests to a host by its configured interval),
// different hosts in parallel. Resumable: each result is appended to .cache/assessment-progress.jsonl, and a course
// already assessed there is skipped. start.sh removes that file whenever it imports a fresh catalogue.
// With --jobs it assesses the live jobs from .cache/live-jobs.json instead, with its own progress file. A feed job's
// checks read only the listing (the feed itself says the job is open), so feed jobs need no per-host pacing.
// Usage: assess-live.mjs [--jobs] [--sample N] [--concurrency K] [--no-grade]   Progress to stderr, a summary to stdout.
import { appendFileSync, existsSync, readFileSync } from "node:fs";
import { adminFetch, runGrading } from "./api.mjs";
import { loadCatalogue, random } from "./live.mjs";

const arg = (name, fallback) => {
  const i = process.argv.indexOf(name);
  return i > 0 ? Number(process.argv[i + 1]) : fallback;
};
const sampleSize = arg("--sample", 0);
const concurrency = arg("--concurrency", 4);
const grade = !process.argv.includes("--no-grade");
const jobs = process.argv.includes("--jobs");
const progressPath = new URL(jobs ? "../.cache/job-assessment-progress.jsonl" : "../.cache/assessment-progress.jsonl", import.meta.url);
const jobsPath = new URL("../.cache/live-jobs.json", import.meta.url);

const hostOf = (url) => {
  try {
    return new URL(url.trim()).hostname.replace(/^www\./, "");
  } catch {
    return "(no link)";
  }
};

const done = new Map();
if (existsSync(progressPath)) {
  for (const line of readFileSync(progressPath, "utf8").split("\n").filter(Boolean)) {
    const result = JSON.parse(line);
    if (result.ok) done.set(result.id, result);
  }
}

const isFeed = (item) => item.syncedInfo?.syncType === "Pull";
const source = jobs ? JSON.parse(readFileSync(jobsPath)).items : loadCatalogue().items;
let items = source.map((item) => ({ id: item.id, host: jobs && isFeed(item) ? `feed:${item.id}` : item.url ? hostOf(item.url) : "(no link)" }));
const byHost = Map.groupBy(items, (item) => item.host);
for (const list of byHost.values()) list.sort((a, b) => random(a.id, "order") - random(b.id, "order"));

// A sample takes a share from every host that grows with the square root of its size, so big hosts lead and small ones still appear.
if (sampleSize) {
  const weights = [...byHost].map(([host, list]) => [host, Math.sqrt(list.length)]);
  const total = weights.reduce((sum, [, w]) => sum + w, 0);
  const picked = [];
  for (const [host, w] of weights.sort((a, b) => b[1] - a[1])) {
    const quota = Math.max(1, Math.round((sampleSize * w) / total));
    picked.push(...byHost.get(host).slice(0, quota));
  }
  items = picked.slice(0, sampleSize);
}

const queues = Map.groupBy(items.filter((item) => !done.has(item.id)), (item) => item.host);
const todo = [...queues.values()].reduce((sum, list) => sum + list.length, 0);
console.error(`Assessing ${todo} live ${jobs ? "jobs" : "courses"} on ${queues.size} hosts (${items.length - todo} already done), ${concurrency} at a time, one per host.`);

const started = Date.now();
const busy = new Set();
const results = [];
const timeByHost = new Map();

async function assess(item) {
  for (let attempt = 1; ; attempt++) {
    const begin = Date.now();
    try {
      const response = await adminFetch(`/courseQuality/opportunity/${item.id}/assess`, { method: "POST", signal: AbortSignal.timeout(180_000) });
      const ms = Date.now() - begin;
      if (response.ok) {
        const assessment = await response.json();
        return { id: item.id, host: item.host, ok: true, ms, status: assessment.status, format: assessment.learnerCard?.format, cost: assessment.learnerCard?.cost?.type };
      }
      if (response.status < 500 || attempt >= 2) return { id: item.id, host: item.host, ok: false, ms, error: `HTTP ${response.status}` };
    } catch (error) {
      if (attempt >= 2) return { id: item.id, host: item.host, ok: false, ms: Date.now() - begin, error: String(error.message ?? error) };
    }
    await new Promise((resolve) => setTimeout(resolve, 5000));
  }
}

function report() {
  const elapsed = (Date.now() - started) / 1000;
  const left = [...queues.values()].reduce((sum, list) => sum + list.length, 0) + busy.size;
  // The run cannot finish before its busiest host does: estimate from that host's remaining courses and average time.
  const longest = Math.max(0, ...[...queues].map(([host, list]) => {
    const t = timeByHost.get(host);
    return (list.length + (busy.has(host) ? 1 : 0)) * (t ? t.ms / t.n : 2500);
  }));
  const eta = Math.max(longest, (left * elapsed * 1000) / Math.max(1, results.length) / concurrency) / 1000;
  console.error(`  ${results.length} of ${todo} done, ${results.filter((r) => !r.ok).length} failed, ${Math.round(elapsed / 60)} min so far, about ${Math.round(eta / 60)} min left`);
}

function next() {
  let best = null;
  for (const [host, list] of queues) if (list.length && !busy.has(host) && (!best || list.length > queues.get(best).length)) best = host;
  if (!best) return null;
  busy.add(best);
  return queues.get(best).shift();
}

async function worker() {
  for (;;) {
    const item = next();
    if (!item) {
      if (busy.size === 0) return;
      await new Promise((resolve) => setTimeout(resolve, 200)); // other hosts are busy: wait for one to free up
      if ([...queues.values()].every((list) => !list.length)) return;
      continue;
    }
    const result = await assess(item);
    busy.delete(item.host);
    appendFileSync(progressPath, JSON.stringify({ ...result, at: new Date().toISOString() }) + "\n");
    results.push(result);
    const t = timeByHost.get(item.host) ?? { n: 0, ms: 0 };
    timeByHost.set(item.host, { n: t.n + 1, ms: t.ms + result.ms });
    if (results.length % 25 === 0) report();
  }
}

await Promise.all(Array.from({ length: concurrency }, worker));
const seconds = Math.round((Date.now() - started) / 1000);

const grading = grade ? await runGrading() : "skipped";

const all = [...done.values(), ...results];
const tally = (key) => Object.entries(Object.groupBy(all.filter((r) => r.ok), (r) => r[key] ?? "-")).map(([k, v]) => `${k} ${v.length}`).join(", ");
const hosts = [...timeByHost].sort((a, b) => b[1].n - a[1].n).slice(0, 8).map(([host, t]) => `${host} ${t.n} x ${(t.ms / t.n / 1000).toFixed(1)} s`);
console.log(`  This run: ${results.length} assessed in ${Math.floor(seconds / 60)}m ${String(seconds % 60).padStart(2, "0")}s, ${results.filter((r) => !r.ok).length} failed`);
console.log(`  All assessed so far: ${all.filter((r) => r.ok).length}. Outcomes: ${tally("status")}`);
console.log(`  Formats: ${tally("format")}. Costs: ${tally("cost")}`);
if (hosts.length) console.log(`  Time per page by host: ${hosts.join("; ")}`);
console.log(`  Grading: ${grading}`);
process.exit(results.some((r) => !r.ok) ? 3 : 0);
