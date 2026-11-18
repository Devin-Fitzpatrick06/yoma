// Sends a practice sample to Yoma. "mock" (default) builds the exact request and returns a preview without sending it.
// "live" calls Yoma with the learner's own Yoma access token: Yoma has no partner endpoint that records a completion.
import type { PracticeSample } from "../schema";
import { verifyForm } from "./evidence";

export type YomaResult =
  | { mode: "mock"; endpoint: string; fields: string[]; skills: string[] }
  | { mode: "live"; endpoint: string; status: number };

export interface YomaConfig {
  mode: "mock" | "live";
  baseUrl: string;
  /** SkillLens course id -> Yoma opportunity id (the opportunity an org admin creates for each course). */
  opportunities: Record<string, string>;
}

export function yomaConfigFromEnv(env: Record<string, string | undefined> = process.env): YomaConfig {
  const mode = (env.YOMA_MODE ?? "mock") as YomaConfig["mode"];
  if (mode !== "mock" && mode !== "live") throw new Error(`Unknown YOMA_MODE "${mode}". Use "mock" or "live".`);
  const opportunities = Object.fromEntries(
    (env.YOMA_OPPORTUNITY_IDS ?? "")
      .split(",")
      .map((pair) => pair.split(":").map((s) => s.trim()))
      .filter((p) => p.length === 2 && p[0] && p[1]),
  );
  if (mode === "live" && Object.keys(opportunities).length === 0)
    throw new Error("YOMA_MODE=live needs YOMA_OPPORTUNITY_IDS, e.g. financial-literacy:<guid>,job-readiness:<guid>");
  return { mode, baseUrl: (env.YOMA_API_BASE_URL ?? "https://v3api.stage.yoma.world/api/v3").replace(/\/+$/, ""), opportunities };
}

const PLACEHOLDER_ID = "00000000-0000-0000-0000-000000000000";

export async function submitToYoma(
  config: YomaConfig,
  sample: PracticeSample,
  courseTitle: string,
  learnerToken?: string,
): Promise<YomaResult> {
  const opportunityId = config.opportunities[sample.courseId] ?? (config.mode === "mock" ? PLACEHOLDER_ID : undefined);
  if (!opportunityId) throw new Error(`No Yoma opportunity configured for course "${sample.courseId}"`);
  const endpoint = `${config.baseUrl}/myOpportunity/action/${opportunityId}/verify`;
  const form = verifyForm(sample, courseTitle);

  if (config.mode === "mock") return { mode: "mock", endpoint, fields: [...new Set(form.keys())], skills: sample.skills.map((s) => s.name) };

  if (!learnerToken) throw new Error("Sending to Yoma needs the learner's Yoma access token");
  const res = await fetch(endpoint, {
    method: "PUT",
    headers: { Authorization: `Bearer ${learnerToken}` },
    body: form,
    signal: AbortSignal.timeout(20_000),
  });
  if (!res.ok) throw new Error(`Yoma verify request failed with status ${res.status}`);
  return { mode: "live", endpoint, status: res.status };
}
