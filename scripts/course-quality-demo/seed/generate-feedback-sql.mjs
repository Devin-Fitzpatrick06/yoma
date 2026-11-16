// Prints SYNTHETIC "Did you take it?" answers as SQL for CourseQuality.LearnerFeedback. Demo data only: no learner wrote them.
// About 300 live courses that demo learners started get 1 to 25 answers each, from learners who left for that course.
// Ratings loosely follow the course's real completion ratio. Tags come from the API's feedback options when it serves them.
// The table and its columns come from the grading migration. If they change, adjust `columns` below and nothing else.
import { readFileSync } from "node:fs";
import { demoLearners } from "./courses.mjs";
import { dayOffset } from "./dates.mjs";
import { learnerIndex, loadCatalogue, participation, random } from "./live.mjs";

const base = process.env.DEMO_BASE_DATE;
if (!base) throw new Error("DEMO_BASE_DATE is not set");
const api = process.env.DEMO_API_URL ?? "http://localhost:5000/api/v3";

// Our field -> the table's column. null leaves a field out.
const table = `"CourseQuality"."LearnerFeedback"`;
const columns = {
  id: "Id", userId: "UserId", opportunityId: "OpportunityId", answer: "Answer", dateLeft: "DateLeft", dateAsked: "DateAsked", snoozeUntil: "SnoozeUntil",
  rating: "Rating", completed: "Completed", wouldRecommend: "WouldRecommend", matchedDescription: "MatchedDescription", good: "Good", bad: "Bad",
  comment: "Comment", dateCreated: "DateCreated", dateModified: "DateModified",
};

// The API's GET feedback/options first, then the API's appsettings.json (mounted read-only in the runner), then placeholders.
let tags = {
  good: ["clear", "practical", "engaging", "good_pace", "useful_certificate"],
  bad: ["outdated", "too_long", "hard_to_follow", "not_as_described", "hidden_costs"],
};
try {
  const response = await fetch(`${api}/courseQuality/feedback/options`, { signal: AbortSignal.timeout(10_000) });
  if (response.ok) {
    const options = await response.json();
    tags = { good: options.good.map((t) => t.key), bad: options.bad.map((t) => t.key), placeholder: false };
  } else console.error(`Feedback options not served (HTTP ${response.status}), so placeholder tags are used.`);
} catch {
  console.error("Feedback options not reachable.");
}
if (tags.placeholder !== false) {
  try {
    const settings = JSON.parse(readFileSync(process.env.DEMO_API_APPSETTINGS ?? "/appsettings.json", "utf8")).CourseQuality.LearnerFeedback;
    if (settings?.Good?.length) {
      tags = { good: settings.Good.map((t) => t.Key), bad: settings.Bad.map((t) => t.Key), placeholder: false };
      console.error("Feedback tags read from the API's appsettings.json.");
    }
  } catch {
    console.error("No feedback tags in the API's appsettings.json either, so placeholder tags are used.");
  }
}

const comments = ["Helpful overview.", "Easy to follow.", "Good examples.", "A bit long.", "Some parts felt dated.", "Useful for my job search.", "Clear lessons.", "Wanted more practice."];
const quote = (value) => (value === null || value === undefined ? "NULL" : `'${String(value).replaceAll("'", "''")}'`);
const json = (list) => `${quote(JSON.stringify(list))}::jsonb`;
const today = dayOffset(base, 0).getTime();
const day = 86_400_000;

const candidates = loadCatalogue()
  .items.map((item) => ({ item, ...participation(item) }))
  .filter((o) => o.started > 0 && Date.parse(o.item.dateStart) < today)
  .sort((a, b) => random(a.item.id, "feedback") - random(b.item.id, "feedback"))
  .slice(0, 300);

const rows = [];
for (const { item, started, ratio } of candidates) {
  const answers = Math.min(25, started, Math.max(1, Math.round(started * (0.4 + 0.5 * random(item.id, "share")))));
  const courseLean = (random(item.id, "lean") - 0.5) * 1.2; // some courses are liked more than their completion suggests
  for (let i = 0; i < answers; i++) {
    const r = (salt) => random(item.id, salt, i);
    const learner = learnerIndex(item, i, demoLearners.length) + 1;
    const dateLeft = today - (2 + Math.floor(r("left") * 120)) * day;
    const roll = r("answer");
    const answer = roll < 0.82 ? "Taken" : roll < 0.9 ? "NotYet" : roll < 0.96 ? "NotTaken" : "Dismissed";
    const row = { answer, userId: learner, dateLeft, dateAsked: dateLeft + (1 + Math.floor(r("asked") * 5)) * day, snoozeUntil: null };
    if (answer === "NotYet") row.snoozeUntil = row.dateAsked + 7 * day;
    if (answer === "Taken") {
      const rating = Math.max(1, Math.min(5, Math.round(2.4 + 2.2 * ratio + courseLean + (r("rating") - 0.5) * 2)));
      const pick = (list, count, salt) => [...list].sort((a, b) => random(item.id, salt, i, a) - random(item.id, salt, i, b)).slice(0, count);
      Object.assign(row, {
        rating,
        completed: r("completed") < 0.15 + 0.75 * ratio,
        wouldRecommend: rating >= 4 || (rating === 3 && r("recommend") < 0.4),
        matchedDescription: rating >= 3 ? r("matched") < 0.9 : r("matched") < 0.4,
        good: rating >= 3 ? pick(tags.good, 1 + Math.floor(r("goodCount") * 2), "good") : [],
        bad: rating <= 3 ? pick(tags.bad, rating <= 2 ? 1 + Math.floor(r("badCount") * 2) : Math.floor(r("badCount") * 1.5), "bad") : [],
        comment: r("comment") < 0.2 ? comments[Math.floor(r("which") * comments.length)] : null,
      });
    }
    rows.push({ item, ...row });
  }
}

const stamp = (ms) => (ms === null || ms === undefined ? "NULL" : quote(new Date(ms).toISOString()));
const value = (field, row) => {
  switch (field) {
    case "id": return "gen_random_uuid()";
    case "userId": return `(SELECT "Id" FROM "Entity"."User" WHERE "Email"='demo.learner${String(row.userId).padStart(2, "0")}@example.org')`;
    case "opportunityId": return quote(row.item.id);
    case "dateLeft": case "dateAsked": case "snoozeUntil": return stamp(row[field]);
    case "dateCreated": return stamp(row.dateLeft);
    case "dateModified": return stamp(row.dateAsked);
    case "good": case "bad": return json(row[field] ?? []);
    case "rating": case "completed": case "wouldRecommend": case "matchedDescription": return row[field] ?? "NULL";
    default: return quote(row[field]);
  }
};

const fields = Object.entries(columns).filter(([, column]) => column);
const out = [`\\set ON_ERROR_STOP on`, `BEGIN;`, `CREATE EXTENSION IF NOT EXISTS "pgcrypto";`];
for (let i = 0; i < rows.length; i += 500) {
  out.push(`INSERT INTO ${table}(${fields.map(([, c]) => `"${c}"`).join(",")}) VALUES\n${rows.slice(i, i + 500).map((row) => `(${fields.map(([f]) => value(f, row)).join(", ")})`).join(",\n")};`);
}
out.push(`COMMIT;`);
console.log(out.join("\n"));
console.error(`Synthetic feedback: ${rows.length} answers on ${candidates.length} courses, ${rows.filter((r) => r.answer === "Taken").length} taken.`);
