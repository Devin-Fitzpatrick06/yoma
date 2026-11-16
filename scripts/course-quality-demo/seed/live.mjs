// The live Yoma Learning catalogue, as fetched by fetch-live-catalogue.mjs, and the demo's scaled participation for it.
// Participation is demo data scaled from each listing's real counts, so that ratios between courses survive:
//   started   = scaled(max(countNavigatedExternalLink, participantCountTotal)), at most 40
//   completed = round(started x participantCountCompleted / participantCountTotal), at least 1 if any real completion
//   viewed    = max(started, scaled(countViewed)), at most 50
// scaled(n) keeps n up to 5, then grows with log(n) to the cap at a reference count (50,000 starts, 25,000 views).
import { existsSync, readFileSync } from "node:fs";

export const cataloguePath = new URL("../.cache/live-catalogue.json", import.meta.url);

export function loadCatalogue() {
  if (!existsSync(cataloguePath)) throw new Error("No live catalogue. Run: node scripts/course-quality-demo/seed/fetch-live-catalogue.mjs");
  return JSON.parse(readFileSync(cataloguePath));
}

const scaled = (n, cap, reference) => (n <= 5 ? n : Math.min(cap, Math.round(5 + ((cap - 5) * Math.log(n / 5)) / Math.log(reference / 5))));

export function participation(item) {
  const total = item.participantCountTotal ?? 0;
  const completedReal = item.participantCountCompleted ?? 0;
  const started = scaled(Math.max(item.countNavigatedExternalLink ?? 0, total), 40, 50_000);
  const ratio = total > 0 ? Math.min(1, completedReal / total) : 0;
  const completed = Math.min(started, Math.max(completedReal > 0 ? 1 : 0, Math.round(started * ratio)));
  const viewed = Math.max(started, scaled(item.countViewed ?? 0, 50, 25_000));
  return { started, completed, viewed, ratio };
}

// Deterministic 0..1 from a string, so every run seeds the same demo data.
export function random(...parts) {
  let h = 2166136261;
  for (const c of parts.join("|")) h = Math.imul(h ^ c.charCodeAt(0), 16777619);
  h = Math.imul(h ^ (h >>> 15), 2246822507);
  h = Math.imul(h ^ (h >>> 13), 3266489909);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

// Which demo learners took part in a course: a run of consecutive learners from a per-course offset.
export const learnerIndex = (item, i, learnerCount) => (Math.floor(random(item.id, "offset") * learnerCount) + i) % learnerCount;
