// Downloads every live Learning opportunity from the public, signed-out Yoma search into .cache/live-catalogue.json.
// The body matches what a signed-out learner's opportunities page sends: Not started and Ongoing, not Expired.
// Items already carry organisation, category, country, language and skill names, so no lookup lists are fetched.
// Usage: node seed/fetch-live-catalogue.mjs   One request at a time, about a second apart, with retries on 429 and 5xx.
import { mkdirSync, writeFileSync } from "node:fs";

const endpoint = "https://api.yoma.world/api/v3/opportunity/search";
const learningType = "25f5a835-c3f7-43ca-9840-d372a1d26694";
const pageSize = 50;
const cacheDir = new URL("../.cache/", import.meta.url);
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const body = (pageNumber) => ({ pageNumber, pageSize, types: [learningType], publishedStates: ["NotStarted", "Active"] });

async function page(pageNumber) {
  for (let attempt = 1; ; attempt++) {
    let status;
    try {
      const response = await fetch(endpoint, {
        method: "POST",
        headers: { "content-type": "application/json", accept: "application/json", "user-agent": "YomaCourseQualityDemo/1.0" },
        body: JSON.stringify(body(pageNumber)),
        signal: AbortSignal.timeout(60_000),
      });
      if (response.ok) return await response.json();
      status = response.status;
      if (status !== 429 && status < 500) throw new Error(`Page ${pageNumber}: HTTP ${status} ${await response.text()}`);
      const retryAfter = Number(response.headers.get("retry-after"));
      if (attempt < 6) await sleep(retryAfter > 0 ? retryAfter * 1000 : 2 ** attempt * 1000);
    } catch (error) {
      if (status && status !== 429 && status < 500) throw error;
      if (attempt >= 6) throw error;
      if (!status) await sleep(2 ** attempt * 1000);
    }
    if (attempt >= 6) throw new Error(`Page ${pageNumber}: gave up after ${attempt} tries (HTTP ${status})`);
    console.error(`page ${pageNumber}: retry ${attempt} after HTTP ${status ?? "network error"}`);
  }
}

const fetchedAt = new Date().toISOString();
const byId = new Map();
let totalCount = null;
let rawCount = 0;
for (let pageNumber = 1; ; pageNumber++) {
  const result = await page(pageNumber);
  totalCount ??= result.totalCount;
  rawCount += result.items.length;
  for (const item of result.items) byId.set(item.id, item);
  console.error(`page ${pageNumber}: ${result.items.length} items, ${byId.size} of ${totalCount}`);
  if (result.items.length < pageSize || rawCount >= totalCount) break;
  await sleep(1000);
}

mkdirSync(cacheDir, { recursive: true });
const items = [...byId.values()];
writeFileSync(
  new URL("live-catalogue.json", cacheDir),
  JSON.stringify({ fetchedAt, endpoint, request: body("N"), totalCount, rawCount, uniqueCount: items.length, items }, null, 2),
);
console.error(`Saved ${items.length} unique items (${rawCount} fetched, totalCount ${totalCount}) to .cache/live-catalogue.json`);
if (items.length !== totalCount) {
  console.error("Warning: the unique count does not match totalCount.");
  process.exitCode = 1;
}
