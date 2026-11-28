// Runs Ziqi's real checks on the connected journey's 9 listings (seed/journey.mjs), one at a time, through the admin
// assess endpoint, then grades the catalogue and prints each listing's outcome: status, grade and what held it back.
// start.sh --journey calls it after generate-journey-sql.mjs. Courses are graded A-D, jobs have their own A-D grade,
// events and impact actions get the listing checks only (no grade).
// Checks the two outcomes the journey depends on: opp-course-005's dead link is caught, and the scam job opp-job-002 is
// held back for its fee. Exits 3 when either is not so, or a listing could not be assessed.
// Usage: assess-journey.mjs [--timeout SECONDS]   Progress to stderr, the table to stdout.
import { adminFetch, runGrading } from "./api.mjs";
import { listings } from "./journey.mjs";

const timeoutArg = process.argv.indexOf("--timeout");
// With Ziqi's AI on, a CPU model can take minutes per course; the API itself gives up on the AI at its own limit.
const timeoutMs = (timeoutArg > 0 ? Number(process.argv[timeoutArg + 1]) : 420) * 1000;

const results = [];
for (const listing of listings) {
  const begin = Date.now();
  console.error(`  ${listing.sharedId}: assessing ${listing.title}`);
  try {
    const response = await adminFetch(`/courseQuality/opportunity/${listing.id}/assess`, { method: "POST", signal: AbortSignal.timeout(timeoutMs) });
    if (!response.ok) {
      results.push({ listing, error: `HTTP ${response.status}` });
      continue;
    }
    const a = await response.json();
    results.push({
      listing,
      status: a.status,
      score: a.score,
      ai: a.aiUsed ? a.aiModel ?? "yes" : "no",
      failed: (a.criteria ?? []).filter((c) => c.hardGateFailed).map((c) => c.criterion),
      reasons: a.reviewReasons ?? [],
      seconds: Math.round((Date.now() - begin) / 1000),
    });
  } catch (error) {
    results.push({ listing, error: String(error.message ?? error) });
  }
}

const grading = await runGrading();
for (const result of results.filter((r) => !r.error && ["Learning", "Job"].includes(r.listing.type))) {
  const response = await adminFetch(`/courseQuality/opportunity/${result.listing.id}/grade`);
  if (!response.ok) continue;
  const grade = await response.json().catch(() => null);
  result.grade = grade?.grade ?? null;
  result.gradeLabel = grade?.gradeLabel ?? null;
}

const flags = (r) => (r.failed.length ? r.failed.join(", ") : "-");
console.log(`  ${"Shared id".padEnd(15)} ${"Type".padEnd(12)} ${"Ziqi status".padEnd(12)} ${"Score".padEnd(5)} ${"Grade".padEnd(5)} ${"AI".padEnd(10)} Held back by`);
for (const r of results) {
  const { listing } = r;
  if (r.error) {
    console.log(`  ${listing.sharedId.padEnd(15)} ${listing.type.padEnd(12)} NOT ASSESSED: ${r.error}`);
    continue;
  }
  console.log(`  ${listing.sharedId.padEnd(15)} ${listing.type.padEnd(12)} ${String(r.status).padEnd(12)} ${String(r.score).padEnd(5)} ${String(r.grade ?? "-").padEnd(5)} ${String(r.ai).slice(0, 10).padEnd(10)} ${flags(r)}`);
  for (const reason of r.reasons) console.log(`  ${"".padEnd(15)} ${reason}`);
}
console.log(`  Grading: ${grading}`);

const by = (id) => results.find((r) => r.listing.sharedId === id);
const problems = [];
if (results.some((r) => r.error)) problems.push("a listing could not be assessed");
if (!by("opp-course-005")?.failed?.includes("LinkWorks")) problems.push("opp-course-005's dead link was not caught");
if (!by("opp-job-002")?.failed?.includes("NoScamSignals")) problems.push("the scam job opp-job-002 was not held back");
if (problems.length) console.log(`  NOT AS EXPECTED: ${problems.join("; ")}`);
process.exit(problems.length ? 3 : 0);
