// `npm run setup`: writes .env.local from .env.example for a fresh clone.
// - Uses the real local model (Ollama) if it is running and has the model; otherwise simulated mode, and says how to switch.
// - Generates a random facilitator password (stored in .env.local only, never printed).
// Never overwrites an existing .env.local.
import { randomBytes } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const root = join(__dirname, "..");
const target = join(root, ".env.local");

async function ollamaHas(model: string, base = "http://localhost:11434"): Promise<boolean> {
  try {
    const res = await fetch(`${base}/api/tags`, { signal: AbortSignal.timeout(3_000) });
    const { models } = (await res.json()) as { models?: { name: string }[] };
    return Boolean(models?.some((m) => m.name === model || m.name === `${model}:latest`));
  } catch {
    return false;
  }
}

async function main() {
  if (existsSync(target)) {
    console.log(".env.local already exists: left as it is. Delete it and run `npm run setup` again to start fresh.");
    return;
  }
  let env = readFileSync(join(root, ".env.example"), "utf8");
  const model = /^LOCAL_AI_MODEL=(.+)$/m.exec(env)?.[1].trim() || "gemma3:4b";
  const local = await ollamaHas(model);
  const set = (key: string, value: string) => (env = env.replace(new RegExp(`^${key}=.*$`, "m"), `${key}=${value}`));
  set("AI_PROVIDER", local ? "local" : "mock");
  set("CONTENT_SAFETY_PROVIDER", local ? "model" : "stub");
  set("FACILITATOR_PASSWORD", randomBytes(8).toString("hex"));
  set("PASS_SIGNING_SECRET", randomBytes(32).toString("hex"));
  writeFileSync(target, env);

  console.log("Wrote .env.local");
  console.log(local ? `  AI: REAL local model (${model} through Ollama), safety: keywords + model` : "  AI: SIMULATED (mock). Ollama with the model was not found.");
  if (!local) console.log(`  For real AI: install Ollama (https://ollama.com), run \`ollama pull ${model}\`, delete .env.local and run \`npm run setup\` again.`);
  console.log("  Facilitator password: in .env.local (FACILITATOR_PASSWORD)");
  console.log("Next: npm run dev, then open http://localhost:3000/demo/p2e");
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
