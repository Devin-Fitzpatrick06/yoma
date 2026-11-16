// Signs in as the seeded admin and runs the real assessment on each demo course.
// Usage: assess.mjs [--ids id,id]   Prints one JSON line per course to stdout, progress to stderr.
import { api, token } from "./api.mjs";
import { courses } from "./courses.mjs";

const idsArg = process.argv.indexOf("--ids");
const only = idsArg > 0 ? new Set(process.argv[idsArg + 1].split(",")) : null;

function meets(expect, card) {
  return Object.entries(expect).every(([key, value]) => {
    if (key === "cost") return card.cost?.type === value;
    return card[key] === value;
  });
}

const accessToken = await token();

for (const course of courses.filter((o) => !only || only.has(o.id))) {
  const response = await fetch(`${api}/courseQuality/opportunity/${course.id}/assess`, {
    method: "POST",
    headers: { authorization: `Bearer ${accessToken}` },
    signal: AbortSignal.timeout(60_000),
  });
  if (!response.ok) {
    console.log(JSON.stringify({ id: course.id, ok: false, error: `HTTP ${response.status}` }));
    continue;
  }
  const assessment = await response.json();
  const card = assessment.learnerCard;
  console.log(JSON.stringify({
    id: course.id,
    ok: meets(course.expect, card),
    hasLive: Boolean(course.liveUrl),
    shows: course.shows,
    status: assessment.status,
    format: card.format,
    cost: card.cost?.type,
    selfPaced: card.selfPaced,
    datesConfirmed: card.datesConfirmed,
    fixtureUrl: `http://demo-fixtures.localtest.me/courses/${course.fixture}`,
  }));
}
