import type { Challenge, Feedback, Lang, Locale, PreviousAttempt, Rubric, Sample, Template } from "../schema";
import { createFoundryProvider, foundryConfigFromEnv, localConfigFromEnv, type FoundryConfig } from "./foundry";
import { mockProvider } from "./mock";

export interface ScoreInput {
  challenge: Challenge;
  rubric: Rubric;
  examples: Sample[];
  answer: string;
  lang: Lang;
  /** Level 2 only: the first attempt, so feedback can build on it instead of starting over. */
  previous?: PreviousAttempt;
}

export interface AIProvider {
  /** Fills an educator-approved template with local details. Never generates from a blank prompt. */
  generateChallenge(template: Template, locale: Locale, lang: Lang, seed: number): Promise<Challenge>;
  /** Rubric-based feedback; output must pass the Feedback schema. */
  scoreAnswer(input: ScoreInput): Promise<Feedback>;
  /** Reads handwritten work from a photo and returns it as text. */
  transcribePhoto(image: Uint8Array, mimeType: string): Promise<string>;
}

export type ProviderName = "mock" | "local" | "foundry";

/** The config for a real model, or null in mock mode. Shared with the model-based safety check. */
export function aiConfig(name = process.env.AI_PROVIDER ?? "mock"): FoundryConfig | null {
  if (name === "mock") return null;
  if (name === "local") return localConfigFromEnv();
  if (name === "foundry") return foundryConfigFromEnv();
  throw new Error(`Unknown AI_PROVIDER "${name}". Use "mock", "local" or "foundry".`);
}

export function getProvider(name = process.env.AI_PROVIDER ?? "mock"): AIProvider {
  const config = aiConfig(name);
  return config ? createFoundryProvider(config) : mockProvider;
}
