import type {
  AIProviderKind,
  CourseQualityAIUsage,
  CourseQualityAIUsageSummary,
} from "~/api/models/courseQuality";

// ─────────────────────────────────────────────────────────────────────────────
// Ziqi iteration 5: plain-words formatting for an AI run (reviewer card "AI check") and the admin
// "AI usage" panel. Pure, so `pnpm test` covers it without React.
// ─────────────────────────────────────────────────────────────────────────────

const COUNT_FORMAT = new Intl.NumberFormat("en-US");

const finite = (value: number | null | undefined): number | null =>
  typeof value === "number" && Number.isFinite(value) ? value : null;

/** A whole, non-negative token count with thousands separators: 3214 → "3,214". */
export const formatTokens = (value: number | null | undefined): string =>
  COUNT_FORMAT.format(Math.max(0, Math.round(finite(value) ?? 0)));

/** "3,214 in / 412 out tokens" */
export const formatTokenPair = (input: number, output: number): string =>
  `${formatTokens(input)} in / ${formatTokens(output)} out tokens`;

/**
 * A dollar amount: four decimals under $1 so a single run's fraction of a cent shows
 * ("$0.0412"), two from $1 up ("$12.40").
 */
export const formatUsd = (value: number): string => {
  const amount = Math.max(0, finite(value) ?? 0);
  if (amount === 0) return "$0.00";
  return amount < 1 ? `$${amount.toFixed(4)}` : `$${amount.toFixed(2)}`;
};

export const LOCAL_COST_TEXT = "$0.00 (local, free)";
export const COST_NOT_CONFIGURED_TEXT = "cost not configured";

/** One run's cost: free when local, never guessed when no price is configured. */
export const formatRunCost = (
  usage: Pick<CourseQualityAIUsage, "isLocal" | "costUsd">,
): string => {
  if (usage.isLocal) return LOCAL_COST_TEXT;
  const cost = finite(usage.costUsd);
  return cost == null ? COST_NOT_CONFIGURED_TEXT : formatUsd(cost);
};

/** "<1 s", "42 s", "2 min 5 s", "3 min" */
export const formatDuration = (ms: number | null | undefined): string => {
  const value = finite(ms);
  if (value == null || value < 0) return "–";
  if (value < 1000) return "<1 s";
  const seconds = Math.round(value / 1000);
  if (seconds < 60) return `${seconds} s`;
  const minutes = Math.floor(seconds / 60);
  const rest = seconds % 60;
  return rest === 0 ? `${minutes} min` : `${minutes} min ${rest} s`;
};

/** Elapsed whole seconds while a run is pending: "0 s", "75 s (1 min 15 s)". */
export const formatElapsed = (seconds: number): string => {
  const whole = Math.max(0, Math.floor(seconds));
  return whole < 60
    ? `${whole} s`
    : `${whole} s (${formatDuration(whole * 1000)})`;
};

export const PROVIDER_LABELS: Record<AIProviderKind, string> = {
  None: "No AI",
  Anthropic: "Anthropic",
  OpenAICompatible: "OpenAI-compatible server",
};

/** "qwen2.5:3b on localhost:11434", "claude-sonnet-4-5 via Anthropic" */
export const describeModel = (
  provider: AIProviderKind,
  model: string | null,
  endpointHost: string | null,
): string => {
  const name = model?.trim() || "model not set";
  if (endpointHost?.trim()) return `${name} on ${endpointHost.trim()}`;
  if (provider === "None") return name;
  return `${name} via ${PROVIDER_LABELS[provider]}`;
};

export type AIOutcome = CourseQualityAIUsage["outcome"];

export const OUTCOME_LABELS: Record<AIOutcome, string> = {
  Answered: "Answered",
  Declined: "Declined",
  Failed: "Failed",
  Invalid: "Invalid",
};

/** What the outcome means for the result, in plain words. */
export const OUTCOME_EXPLANATIONS: Record<AIOutcome, string> = {
  Answered:
    "The model answered in the required format. It can only lower a result, never raise it.",
  Declined:
    "The model declined or stopped before it finished, so the rule-based result stands.",
  Failed:
    "The model could not be reached or the call failed, so the rule-based result stands.",
  Invalid:
    "The model did not answer in the required format, so the rule-based result stands.",
};

/** Answered reads as done; the other outcomes mean the AI did not count. */
export const OUTCOME_BADGES: Record<AIOutcome, string> = {
  Answered: "bg-green-light text-green",
  Declined: "bg-yellow-tint text-black",
  Failed: "bg-orange-light text-black",
  Invalid: "bg-orange-light text-black",
};

export const NO_AI_TEXT = "No AI ran for this check (rule-based reader)";

/**
 * The run line on the reviewer card:
 * "Local model · qwen2.5:3b on localhost:11434 · 3,214 in / 412 out tokens · $0.00 (local, free) · 42 s · Answered"
 */
export const formatRunLine = (usage: CourseQualityAIUsage): string =>
  [
    usage.isLocal ? "Local model" : "Remote model",
    describeModel(usage.provider, usage.model, usage.endpointHost),
    formatTokenPair(usage.inputTokens, usage.outputTokens),
    formatRunCost(usage),
    formatDuration(usage.durationMs),
    OUTCOME_LABELS[usage.outcome] ?? usage.outcome,
  ].join(" · ");

export const LOCAL_BADGE_TEXT = "Local · free";
export const REMOTE_BADGE_TEXT = "Remote · billed";

/** "1 run", "3 runs" */
const runs = (count: number): string =>
  `${formatTokens(count)} ${count === 1 ? "run" : "runs"}`;

/**
 * The admin panel's total cost, from the stored history rather than the model configured now:
 * - "$0.0412 known · 3 runs without a price" whenever some runs have no price, so the total never
 *   reads as complete when it is not;
 * - "$0.00 (local, free)" only when every run was local and nothing was billed;
 * - otherwise the known total.
 */
export const formatTotalCost = (
  summary: Pick<
    CourseQualityAIUsageSummary,
    "isLocal" | "costUsd" | "unpricedRuns" | "remoteRuns"
  >,
): string => {
  const unpriced = Math.max(0, Math.round(finite(summary.unpricedRuns) ?? 0));
  if (unpriced > 0)
    return `${formatUsd(summary.costUsd)} known · ${runs(unpriced)} without a price`;

  const remote = Math.max(0, Math.round(finite(summary.remoteRuns) ?? 0));
  return summary.isLocal && remote === 0 && !(finite(summary.costUsd) ?? 0)
    ? LOCAL_COST_TEXT
    : formatUsd(summary.costUsd);
};

/** What to set in the API configuration when no model is configured. */
export const CONFIGURE_AI_STEPS: { setting: string; example: string }[] = [
  { setting: "AI:Provider", example: "OpenAICompatible (or Anthropic)" },
  {
    setting: "AI:OpenAICompatible:BaseUrl",
    example: "http://localhost:11434/v1 (Ollama)",
  },
  { setting: "AI:OpenAICompatible:Model", example: "qwen2.5:3b" },
  {
    setting: "AI:OpenAICompatible:IsLocal",
    example: "true for your own host (cost 0); default false",
  },
];
