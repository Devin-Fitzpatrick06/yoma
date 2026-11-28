// Downloads every live Learning opportunity from the public, signed-out Yoma search into .cache/live-catalogue.json,
// with --jobs every live Job into .cache/live-jobs.json (jobs carry their feed partner in syncedInfo), or with --tasks
// every live Task into .cache/live-tasks.json. Live Yoma's "Task" (f12a9d90-…) is the type this branch renames in place
// to ImpactAction ("Impact Action"), keeping the id, so the demo imports tasks as impact actions. With --events it
// downloads every live Event into .cache/live-events.json (type "Event", the same name and id in this branch).
// The body matches what a signed-out learner's opportunities page sends: Not started and Ongoing, not Expired.
// Items already carry organisation, category, country, language and skill names, so no lookup lists are fetched.
// Usage: node seed/fetch-live-catalogue.mjs [--jobs | --tasks | --events] [--max N]   One request at a time, about a second apart, with retries on 429 and 5xx.
import { mkdirSync, writeFileSync } from "node:fs";

const endpoint = "https://api.yoma.world/api/v3/opportunity/search";
const jobs = process.argv.includes("--jobs");
const tasks = process.argv.includes("--tasks");
const events = process.argv.includes("--events");
if ([jobs, tasks, events].filter(Boolean).length > 1) throw new Error("Choose one of --jobs, --tasks and --events.");
// The live public type ids (GET /api/v3/opportunity/type): Job, Task (ImpactAction here), Event or Learning
const type = jobs ? "9c2d1a8e-3a4b-4f7a-9e2d-7f1c6b8a2d55" : tasks ? "f12a9d90-a8f6-4914-8ca5-6acf209f7312"
  : events ? "e20dcceb-59c3-46d0-ab0a-d321d8bc4c31" : "25f5a835-c3f7-43ca-9840-d372a1d26694";
const fileName = jobs ? "live-jobs.json" : tasks ? "live-tasks.json" : events ? "live-events.json" : "live-catalogue.json";
// --max N stops after N items, e.g. for a quick look at jobs; by default everything is fetched
const maxIndex = process.argv.indexOf("--max");
const max = maxIndex > 0 ? Number(process.argv[maxIndex + 1]) : Infinity;
const pageSize = 50;
const cacheDir = new URL("../.cache/", import.meta.url);
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const body = (pageNumber) => ({ pageNumber, pageSize, types: [type], publishedStates: ["NotStarted", "Active"] });

async function page(pageNumber) {
  for (let attempt = 1; ; attempt++) {
    let status;
    try {
      const response = await fetch(endpoint, {
        method: "POST",
        headers: { "content-type": "application/json", accept: "application/json", "user-agent": "YomaCourseQualityDemo/1.0" },
        body: JSON.stringify(body(pageNumber)),
        signal: AbortSignal.timeout(60_000),
      });
      if (response.ok) return await response.json();
      status = response.status;
      if (status !== 429 && status < 500) throw new Error(`Page ${pageNumber}: HTTP ${status} ${await response.text()}`);
      const retryAfter = Number(response.headers.get("retry-after"));
      if (attempt < 6) await sleep(retryAfter > 0 ? retryAfter * 1000 : 2 ** attempt * 1000);
    } catch (error) {
      if (status && status !== 429 && status < 500) throw error;
      if (attempt >= 6) throw error;
      if (!status) await sleep(2 ** attempt * 1000);
    }
    if (attempt >= 6) throw new Error(`Page ${pageNumber}: gave up after ${attempt} tries (HTTP ${status})`);
    console.error(`page ${pageNumber}: retry ${attempt} after HTTP ${status ?? "network error"}`);
  }
}

const fetchedAt = new Date().toISOString();
const byId = new Map();
let totalCount = null;
let rawCount = 0;
for (let pageNumber = 1; ; pageNumber++) {
  const result = await page(pageNumber);
  totalCount ??= result.totalCount;
  rawCount += result.items.length;
  for (const item of result.items) byId.set(item.id, item);
  console.error(`page ${pageNumber}: ${result.items.length} items, ${byId.size} of ${totalCount}`);
  if (result.items.length < pageSize || rawCount >= totalCount || byId.size >= max) break;
  await sleep(1000);
}

mkdirSync(cacheDir, { recursive: true });
const items = [...byId.values()];
writeFileSync(
  new URL(fileName, cacheDir),
  JSON.stringify({ fetchedAt, endpoint, request: body("N"), totalCount, rawCount, uniqueCount: items.length, items }, null, 2),
);
console.error(`Saved ${items.length} unique items (${rawCount} fetched, totalCount ${totalCount}) to .cache/${fileName}`);
if (items.length !== totalCount && max === Infinity) {
  console.error("Warning: the unique count does not match totalCount.");
  process.exitCode = 1;
}
