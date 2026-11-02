// Prints the result of the assessment run. Input: the JSON lines from assess.mjs, later lines win.
// A course shows as live only if it has a real URL, was not switched to a demo page, and the run was not --fixtures.
// Only the named courses and any course that missed its expected state are listed, so the table stays short.
import { readFileSync } from "node:fs";
import { courses } from "./courses.mjs";

const fellBack = new Set((process.env.FALLBACK_IDS ?? "").split(" ").filter(Boolean));
const allFixtures = process.env.USE_FIXTURES === "true";

const results = new Map();
for (const line of readFileSync(0, "utf8").split("\n").filter(Boolean)) {
  const result = JSON.parse(line);
  results.set(result.id, result);
}

const tally = (values) => {
  const counts = new Map();
  for (const value of values) counts.set(value, (counts.get(value) ?? 0) + 1);
  return [...counts].sort(([a], [b]) => a.localeCompare(b));
};

const all = [...results.values()];
const missed = all.filter((o) => !o.ok);
console.log(`  ${all.length} courses assessed, ${all.length - missed.length} as expected, ${missed.length} not as expected`);
console.log(`  Outcomes: ${tally(all.map((o) => o.status ?? "no result")).map(([status, count]) => `${status} ${count}`).join(", ")}`);
console.log(`  Formats:  ${tally(all.map((o) => o.format ?? "no result")).map(([format, count]) => `${format} ${count}`).join(", ")}`);

const assessed = courses.filter((o) => results.has(o.id));
console.log(`\n  Categories:`);
for (const [category, count] of tally(assessed.map((o) => o.category))) console.log(`  ${String(count).padStart(4)}  ${category}`);

console.log(`\n  Named courses, and any course not as expected:`);
for (const course of courses) {
  const result = results.get(course.id);
  if (!result || (!course.named && result.ok)) continue;

  const source = !result.hasLive || allFixtures || fellBack.has(result.id) ? "demo page" : "live page";
  const outcome = result.ok ? "as expected" : `NOT AS EXPECTED ${result.error ?? ""}`.trim();
  console.log(`  ${course.id.slice(-3)} ${course.title.slice(0, 46).padEnd(46)} ${(result.status ?? "-").padEnd(12)} ${(result.format ?? "-").padEnd(8)} ${(result.cost ?? "-").padEnd(8)} ${source.padEnd(10)} ${outcome}`);
}

process.exit(missed.length === 0 ? 0 : 3);
