// Scores the hand-rated validation sample (zeki-vs-current-review.md, steps 3 to 6) and prints a Markdown report.
// Usage: node validation/score.mjs [DIR]   DIR holds key.csv, rater-a.csv and rater-b.csv (default .cache/validation/)
import { readFileSync } from "node:fs";
import { bootstrap, fromCsv, kappa, seeded } from "./lib.mjs";

const dir = process.argv[2] ?? new URL("../.cache/validation/", import.meta.url).pathname;
const read = (name) => fromCsv(readFileSync(`${dir}/${name}`, "utf8"));
const key = new Map(read("key.csv").map((r) => [r.id, r]));
const a = new Map(read("rater-a.csv").map((r) => [r.id, r]));
const b = new Map(read("rater-b.csv").map((r) => [r.id, r]));

const norm = (v) => String(v ?? "").trim().toLowerCase();
const YES_NO = ["yes", "no", "cannot tell"];
const BANDS = ["basic", "good", "strong", "top"]; // ordered low to high
const GRADE_BAND = { D: "basic", C: "good", B: "strong", A: "top" };
const questions = [
  ["live", YES_NO], ["link_works", YES_NO], ["dates_match", YES_NO], ["page_matches", YES_NO], ["format", ["course", "webinar"]], ["quality_band", BANDS],
];

const ids = [...key.keys()].filter((id) => a.has(id) && b.has(id));
const problems = [];
for (const id of ids) for (const [q, allowed] of questions) for (const [who, sheet] of [["A", a], ["B", b]]) {
  if (!allowed.includes(norm(sheet.get(id)[q]))) problems.push(`${who} ${id} ${q}="${sheet.get(id)[q]}"`);
}
if (problems.length) {
  console.error(`Fix these ${problems.length} answers first (allowed values are in draw-sample.mjs):\n${problems.slice(0, 20).join("\n")}`);
  process.exit(1);
}

const random = seeded(7);
const fmt = (v) => (v === null ? "n/a" : v.toFixed(2));
const ci = (v) => (v ? `${v[0].toFixed(2)} to ${v[1].toFixed(2)}` : "n/a");
const lines = [`# Validation sample results`, ``, `Listings rated by both raters: ${ids.length} of ${key.size}.`, ``];

// Step 3: agreement between raters
lines.push(`## 1. Do the raters agree? (Cohen's kappa; below 0.60, refine the guide and re-rate before comparing)`, ``, `| Question | kappa | 95% interval |`, `| --- | --- | --- |`);
let ratersReliable = true;
for (const [q, allowed] of questions) {
  const pairs = ids.map((id) => [norm(a.get(id)[q]), norm(b.get(id)[q])]);
  const scale = q === "quality_band" ? BANDS : null;
  const stat = (ps) => kappa(ps.map((p) => p[0]), ps.map((p) => p[1]), scale ?? allowed);
  const k = stat(pairs);
  if (k !== null && k < 0.6) ratersReliable = false;
  // kappa is undefined when every answer is the same; then the raters simply agreed on all of them
  const allSame = k === null && pairs.every((p) => p[0] === p[1]);
  lines.push(`| ${q}${scale ? " (weighted)" : ""} | ${allSame ? "all agreed" : fmt(k)} | ${allSame ? "" : ci(bootstrap(pairs, stat, random))} |`);
}
lines.push(``, ratersReliable ? `Raters agree well enough to compare.` : `**At least one question is below 0.60: do not compare Zeki yet.**`, ``);

// Step 4: Zeki against the raters, on listings where both raters agree
const pass = (r) => ["live", "link_works", "dates_match", "page_matches"].every((q) => norm(r[q]) === "yes");
const decided = ids.filter((id) => pass(a.get(id)) === pass(b.get(id)) && key.get(id).zeki_status && key.get(id).zeki_status !== "NeedsReview");
const verdicts = decided.map((id) => [key.get(id).zeki_status === "Verified" ? "pass" : "fail", pass(a.get(id)) ? "pass" : "fail"]);
const vStat = (ps) => kappa(ps.map((p) => p[0]), ps.map((p) => p[1]), ["pass", "fail"]);
const share = (ps) => (ps.length ? ps.filter((p) => p[0] === p[1]).length / ps.length : null);
const table = { "pass/pass": 0, "pass/fail": 0, "fail/pass": 0, "fail/fail": 0 };
for (const [z, r] of verdicts) table[`${z}/${r}`]++;
lines.push(`## 2. Zeki's Verified against the raters (listings both raters agree on, Zeki decided: ${decided.length})`, ``,
  `kappa ${fmt(vStat(verdicts))}, 95% interval ${ci(bootstrap(verdicts, vStat, random))}; ` +
  `agreement ${fmt(share(verdicts))}, 95% interval ${ci(bootstrap(verdicts, share, random))}`, ``,
  `| Zeki \\ raters | pass | fail |`, `| --- | --- | --- |`, `| Verified | ${table["pass/pass"]} | **${table["pass/fail"]}** (Zeki missed a problem) |`,
  `| Held back | ${table["fail/pass"]} | ${table["fail/fail"]} |`, ``);

const graded = ids.filter((id) => GRADE_BAND[key.get(id).zeki_grade] && norm(a.get(id).quality_band) === norm(b.get(id).quality_band));
const bands = graded.map((id) => [GRADE_BAND[key.get(id).zeki_grade], norm(a.get(id).quality_band)]);
const gStat = (ps) => kappa(ps.map((p) => p[0]), ps.map((p) => p[1]), BANDS);
lines.push(`## 3. Zeki's grade against the raters' band (weighted kappa, graded listings the raters agree on: ${graded.length})`, ``,
  `kappa ${fmt(gStat(bands))}, 95% interval ${ci(bootstrap(bands, gStat, random))}`, ``);

// Step 5: the existing review approves every live listing, so on the same listings it agrees with the raters exactly
// where they pass. Its kappa is undefined (it never says "fail"), so the like-for-like measure is agreement.
const existingPairs = decided.map((id) => ["pass", pass(a.get(id)) ? "pass" : "fail"]);
const zeki = share(verdicts), current = share(existingPairs);
const zekiCi = bootstrap(verdicts, share, random), currentCi = bootstrap(existingPairs, share, random);
// paired difference on the same listings, so the interval reflects that both are judged against the same raters
const paired = decided.map((_, i) => [verdicts[i], existingPairs[i]]);
const diff = (ps) => (ps.length ? share(ps.map((p) => p[0])) - share(ps.map((p) => p[1])) : null);
const diffCi = bootstrap(paired, diff, random);
lines.push(`## 4. Zeki against the existing review, on the same ${decided.length} listings`, ``,
  `| | Agreement with the raters | 95% interval |`, `| --- | --- | --- |`,
  `| Zeki | ${fmt(zeki)} | ${ci(zekiCi)} |`, `| Existing review (approves every live listing) | ${fmt(current)} | ${ci(currentCi)} |`,
  `| Difference (Zeki minus existing) | ${fmt(diff(paired))} | ${ci(diffCi)} |`, ``,
  !ratersReliable ? `**No conclusion: the raters do not agree well enough (section 1).**`
    : diffCi && diffCi[0] > 0 ? `Zeki agrees with the raters more often than the existing review: the whole interval of the difference is above 0.`
    : `No clear difference: the interval of the difference includes 0, so do not say Zeki is better.`, ``);

// Step 6: disagreements by failure type
const missed = decided.filter((id) => key.get(id).zeki_status === "Verified" && !pass(a.get(id)));
const byType = {};
for (const id of missed) for (const q of ["live", "link_works", "dates_match", "page_matches"]) if (norm(a.get(id)[q]) !== "yes") byType[q] = (byType[q] ?? 0) + 1;
lines.push(`## 5. Where Zeki Verified a listing the raters failed`, ``, ...Object.entries(byType).map(([q, n]) => `- ${q}: ${n}`), missed.length ? "" : "- none", ``);

console.log(lines.join("\n"));
