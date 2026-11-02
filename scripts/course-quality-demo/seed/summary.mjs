// Prints the result of the assessment run. Input: the JSON lines from assess.mjs, later lines win.
// A course shows as live only if it has a real URL, was not switched to a demo page, and the run was not --fixtures.
import { readFileSync } from "node:fs";

const fellBack = new Set((process.env.FALLBACK_IDS ?? "").split(" ").filter(Boolean));
const allFixtures = process.env.USE_FIXTURES === "true";

const results = new Map();
for (const line of readFileSync(0, "utf8").split("\n").filter(Boolean)) {
  const result = JSON.parse(line);
  results.set(result.id, result);
}

for (const result of results.values()) {
  const source = !result.hasLive || allFixtures || fellBack.has(result.id) ? "demo page" : "live page";
  const outcome = result.ok ? "as expected" : `NOT AS EXPECTED ${result.error ?? ""}`.trim();
  console.log(`  ${(result.shows ?? result.id).padEnd(34)} ${(result.status ?? "-").padEnd(12)} ${(result.format ?? "-").padEnd(8)} ${(result.cost ?? "-").padEnd(8)} ${source.padEnd(10)} ${outcome}`);
}

process.exit([...results.values()].every((o) => o.ok) ? 0 : 3);
