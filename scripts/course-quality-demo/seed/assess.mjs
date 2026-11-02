// Signs in as the seeded admin and runs the real assessment on each demo course.
// Usage: assess.mjs [--ids id,id]   Prints one JSON line per course to stdout, progress to stderr.
import { readFileSync } from "node:fs";

const { courses } = JSON.parse(readFileSync(new URL("./demo-courses.json", import.meta.url)));
const api = "http://localhost:5000/api/v3";
const idsArg = process.argv.indexOf("--ids");
const only = idsArg > 0 ? new Set(process.argv[idsArg + 1].split(",")) : null;

async function token() {
  const body = new URLSearchParams({
    grant_type: "password",
    client_id: process.env.KEYCLOAK_CLIENT_ID,
    client_secret: process.env.KEYCLOAK_CLIENT_SECRET,
    username: "testadminuser@gmail.com",
    password: "P@ssword12", // public seed default from src/api/docker-compose.yml
    scope: "openid profile email yoma-api",
  });
  const response = await fetch("http://localhost:8080/realms/yoma/protocol/openid-connect/token", { method: "POST", body });
  if (!response.ok) throw new Error(`Could not sign in as the demo admin (${response.status})`);
  return (await response.json()).access_token;
}

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
