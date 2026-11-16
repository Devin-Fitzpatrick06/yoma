// Signs in as the seeded demo admin. Runs where localhost is the demo's gateway (the web or runner container).
export const api = process.env.DEMO_API_URL ?? "http://localhost:5000/api/v3";

export async function token() {
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

// An admin request that signs in again when the token is a few minutes old or refused, for runs that take hours.
let current = null;
export async function adminFetch(path, init = {}) {
  for (let attempt = 0; attempt < 2; attempt++) {
    if (!current || Date.now() - current.at > 4 * 60_000 || attempt > 0) current = { value: await token(), at: Date.now() };
    const response = await fetch(`${api}${path}`, { ...init, headers: { ...init.headers, authorization: `Bearer ${current.value}` } });
    if (response.status !== 401) return response;
  }
  throw new Error(`${path}: refused after signing in again`);
}

// Grades the whole catalogue. Says so plainly when this API has no grading yet.
export async function runGrading() {
  const response = await adminFetch("/courseQuality/grading/run", { method: "POST", signal: AbortSignal.timeout(600_000) });
  if (response.ok) return "done";
  return response.status === 404 || response.status === 405 ? "not available in this API yet" : `failed, HTTP ${response.status}`;
}
