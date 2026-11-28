// One live AI check through the Yoma backend: signs in as the demo admin, runs the real assessment on one course
// (POST /courseQuality/opportunity/{id}/assess) and prints what the AI used: model, tokens, cost, duration, outcome.
// Run by ai-smoke.sh in the runner container, where localhost is the demo's gateway.
// Usage: ai-smoke.mjs [101..108 | opportunity id]   Default 102, a named course whose demo page is always there.
import { readFileSync } from "node:fs";
import { adminFetch } from "./api.mjs";

const arg = process.argv[2] ?? "102";
const id = /^1\d\d$/.test(arg) ? `c0a7e000-0000-4000-8000-000000000${arg}` : arg;
if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) {
  console.error(`"${arg}" is neither a named course number (101 to 108) nor an opportunity id.`);
  process.exit(2);
}
const named = JSON.parse(readFileSync(new URL("./demo-courses.json", import.meta.url))).courses.find((c) => c.id === id);
console.log(`Course:   ${named ? `${id.slice(-3)} ${named.title}` : id}`);
console.log("Running Ziqi's assessment with the AI (on a CPU a small local model takes roughly 1 to 3 minutes)...");

const started = Date.now();
const response = await adminFetch(`/courseQuality/opportunity/${id}/assess`, { method: "POST", signal: AbortSignal.timeout(600_000) });
if (!response.ok) {
  console.error(`The assessment failed: HTTP ${response.status} ${(await response.text()).slice(0, 300)}`);
  process.exit(1);
}
const assessment = await response.json();
const usage = assessment.aiUsage;
console.log(`Result:   ${assessment.status} (round trip ${((Date.now() - started) / 1000).toFixed(1)} s)`);
if (!usage) {
  console.error("No AI ran for this assessment (aiUsage is empty). Check that this API build has the iteration 5 AI provider,");
  console.error("that the demo was started with --local-ai or --ai-url, and the API log: docker compose -p yoma-course-quality-demo logs api");
  process.exit(1);
}

const cost = usage.costUsd === null || usage.costUsd === undefined
  ? "unknown (no price is configured for this model)"
  : `$${usage.costUsd.toFixed(usage.costUsd > 0 && usage.costUsd < 0.01 ? 6 : 2)}${usage.isLocal ? " (local model, free)" : ""}`;
console.log(`Provider: ${usage.provider}${usage.endpointHost ? ` at ${usage.endpointHost}` : ""}${usage.isLocal ? ", local" : ", remote"}`);
console.log(`Model:    ${usage.model}`);
console.log(`Tokens:   ${usage.inputTokens} in, ${usage.outputTokens} out`);
console.log(`Cost:     ${cost}`);
console.log(`Duration: ${(usage.durationMs / 1000).toFixed(1)} s`);
console.log(`Outcome:  ${usage.outcome}`);

// The running totals, if this API serves them.
const summary = await adminFetch("/courseQuality/ai/usage", { signal: AbortSignal.timeout(30_000) }).catch(() => null);
if (summary?.ok) {
  const s = await summary.json();
  console.log(`All runs: ${s.runs} (${s.answered} answered), ${s.inputTokens} tokens in, ${s.outputTokens} out, $${Number(s.costUsd ?? 0).toFixed(2)} known cost`);
}
process.exit(usage.outcome === "Answered" ? 0 : 3);
