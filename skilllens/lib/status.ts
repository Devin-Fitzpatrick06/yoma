// What is real and what is simulated right now, checked live. Shown at /status and on the demo host page,
// so a demo never passes simulated output off as real.
import { aiConfig } from "./ai/provider";
import { PROMPT_VERSION } from "./ai/prompts";
import skillsCache from "../data/yoma/skills.json";

export type Mode = "real" | "simulated";
export interface Integration {
  name: string;
  mode: Mode;
  detail: string;
  healthy: boolean | null; // null = not checked (nothing to reach)
}

async function reachable(url: string, init?: RequestInit): Promise<boolean> {
  try {
    const res = await fetch(url, { ...init, signal: AbortSignal.timeout(4_000) });
    return res.ok;
  } catch {
    return false;
  }
}

export async function integrationStatus(env: Record<string, string | undefined> = process.env): Promise<Integration[]> {
  const ai = (() => {
    try {
      return aiConfig(env.AI_PROVIDER);
    } catch {
      return null;
    }
  })();
  const yomaBase = (env.YOMA_API_BASE_URL ?? "https://v3api.stage.yoma.world/api/v3").replace(/\/+$/, "");
  const safety = env.CONTENT_SAFETY_PROVIDER ?? "stub";
  const [aiUp, yomaUp] = await Promise.all([
    ai ? reachable(`${ai.baseUrl}/models`, { headers: { "api-key": ai.apiKey } }) : Promise.resolve(null),
    env.YOMA_OPPORTUNITIES === "snapshot" ? Promise.resolve(null) : reachable(`${yomaBase}/lookup/skill?nameContains=budget&pageNumber=1&pageSize=1`),
  ]);

  return [
    {
      name: "AI model (challenges, feedback, photo reading)",
      mode: ai ? "real" : "simulated",
      detail: ai ? `${ai.label}: ${ai.chatModel} at ${ai.baseUrl}, prompts ${PROMPT_VERSION}` : "Mock provider: scores by similarity to educator examples, feedback from rubric text",
      healthy: aiUp,
    },
    {
      name: "Content safety",
      mode: safety === "stub" ? "simulated" : "real",
      detail:
        safety === "azure"
          ? "Azure AI Content Safety (text and images)"
          : safety === "model"
            ? `Keyword list, then the AI model as a classifier (text only; ${ai?.chatModel ?? "no model configured"})`
            : "Keyword list only: not a safety system",
      healthy: safety === "model" ? aiUp : null,
    },
    {
      name: "Personal-detail stripping",
      mode: "real",
      detail: "Rules for phone numbers, emails, addresses, names, handles (runs in SkillLens, no external service)",
      healthy: null,
    },
    {
      name: "Yoma opportunities (Next steps)",
      mode: env.YOMA_OPPORTUNITIES === "snapshot" ? "simulated" : "real",
      detail: env.YOMA_OPPORTUNITIES === "snapshot" ? "Snapshot file only" : `Live search at ${yomaBase}; snapshot if unreachable`,
      healthy: yomaUp,
    },
    {
      name: "Yoma skills",
      mode: "real",
      detail: `${skillsCache.skills.length} skill ids from ${skillsCache.source}, cached ${skillsCache.fetchedAt}`,
      healthy: null,
    },
    {
      name: "Yoma profile (save practice sample)",
      mode: env.YOMA_MODE === "live" ? "real" : "simulated",
      detail: env.YOMA_MODE === "live" ? `Sends to ${yomaBase} with the learner's Yoma token` : "Builds the exact Yoma request and shows a preview; sends nothing",
      healthy: null,
    },
  ];
}
