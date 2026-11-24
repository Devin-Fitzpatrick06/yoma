// Draws the hand-rated validation sample: 100 live Learning listings, stratified by provider (Alison and the rest,
// at least 30 non-Alison) and by Zeki result (grades A–D, waiting for review, Not verified), from a fixed seed.
// Writes three files to the output folder:
//   key.csv       Zeki's result per listing. Keep it away from the raters until both have finished.
//   rater-a.csv   blank rating sheet, listings in one random order, no Zeki result
//   rater-b.csv   the same listings in a different random order
// Usage: ./sample.sh [--seed N] [--size 100] [--out DIR]   (sample.sh feeds this script the listings as JSON on stdin)
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { seeded, shuffle, toCsv } from "./lib.mjs";

const arg = (name, fallback) => {
  const i = process.argv.indexOf(name);
  return i > 0 ? process.argv[i + 1] : fallback;
};
const seed = Number(arg("--seed", 20261124));
const size = Number(arg("--size", 100));
const minOthers = Math.min(Number(arg("--min-non-alison", 30)), size);
const out = arg("--out", new URL("../.cache/validation/", import.meta.url).pathname);

const listings = JSON.parse(readFileSync(0, "utf8")) ?? [];
if (!listings.length) {
  console.error("No live Learning listings found. Is the demo database loaded?");
  process.exit(1);
}

const stratum = (l) => (l.status === "Verified" && l.grade ? `Grade ${l.grade}` : l.status === "Verified" ? "Verified, ungraded" : l.status ?? "Not assessed");
const group = (l) => (/^alison/i.test(l.organization ?? "") ? "Alison" : "Other providers");
const random = seeded(seed);

// provider split: in proportion, but at least minOthers from the other providers when they have that many
const byGroup = Map.groupBy(listings, group);
const others = byGroup.get("Other providers") ?? [];
const alison = byGroup.get("Alison") ?? [];
let otherQuota = Math.max(minOthers, Math.round((size * others.length) / listings.length));
otherQuota = Math.min(otherQuota, others.length);
const quotas = new Map([["Other providers", otherQuota], ["Alison", Math.min(size - otherQuota, alison.length)]]);

// within a provider group, spread evenly over the Zeki strata so rare results (grade A, Not verified) are represented
const picked = [];
for (const [name, quota] of quotas) {
  const strata = [...Map.groupBy(shuffle(byGroup.get(name) ?? [], random), stratum).entries()].sort(([a], [b]) => a.localeCompare(b));
  let left = quota;
  let round = 0;
  while (left > 0 && strata.some(([, items]) => items.length > round)) {
    for (const [, items] of strata) {
      if (left === 0) break;
      if (items.length > round) { picked.push(items[round]); left--; }
    }
    round++;
  }
}

mkdirSync(out, { recursive: true });
const key = picked.map((l) => ({ id: l.id, provider_group: group(l), stratum: stratum(l), zeki_status: l.status ?? "", zeki_grade: l.grade ?? "" }));
writeFileSync(`${out}/key.csv`, toCsv(key, ["id", "provider_group", "stratum", "zeki_status", "zeki_grade"]));

const sheetColumns = ["id", "title", "provider", "url", "live", "link_works", "dates_match", "format", "page_matches", "quality_band", "notes"];
const sheet = (order) => order.map((l) => ({ id: l.id, title: l.title, provider: l.organization, url: l.url }));
writeFileSync(`${out}/rater-a.csv`, toCsv(sheet(shuffle(picked, random)), sheetColumns));
writeFileSync(`${out}/rater-b.csv`, toCsv(sheet(shuffle(picked, random)), sheetColumns));

const counts = Object.entries(Object.groupBy(key, (k) => `${k.provider_group}: ${k.stratum}`)).map(([k, v]) => `  ${k.padEnd(40)} ${v.length}`);
console.log(`Drew ${picked.length} of ${listings.length} live listings with seed ${seed} into ${out}\n${counts.sort().join("\n")}`);
console.log(`
Raters fill: live, link_works, dates_match, page_matches = yes | no | cannot tell; format = course | webinar;
quality_band = top | strong | good | basic. Give rater-a.csv and rater-b.csv to different people; keep key.csv back.
Then: node validation/score.mjs ${out}`);
