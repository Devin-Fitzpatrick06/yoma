// The one path every learner input and model output takes: strip personal details, screen, call the model, screen again.
// Used by the API routes and by the eval harness, so the eval measures exactly what learners get.
import { aiConfig, getProvider, type AIProvider } from "./ai/provider";
import { mockProvider } from "./ai/mock";
import { evidenceFor, pickSuggestion, tipFor } from "./evidence";
import { getSafety, type ContentSafety, type SafetyResult } from "./safety/contentSafety";
import { groundStrengths } from "./safety/grounding";
import { redactPii, type PiiKind } from "./safety/pii";
import type { Challenge, Feedback, Lang, Locale, PreviousAttempt, Rubric, Sample, Template } from "./schema";
import { addFlag } from "./store";

export interface Deps {
  provider: AIProvider;
  safety: ContentSafety;
}

export const defaultDeps = (): Deps => ({ provider: getProvider(), safety: getSafety(process.env, aiConfig()) });

export type Flagged = { status: "flagged"; categories: SafetyResult["categories"] };

export type ScoreOutcome =
  | {
      status: "ok";
      feedback: Feedback;
      /** The answer with personal details removed: the only version that is ever saved. */
      answer: string;
      redacted: PiiKind[];
      /** True when the model's feedback was flagged and replaced with the educator-written rubric text. */
      fallback: boolean;
      /** "Did well" items dropped because they praised something the answer never said. */
      ungrounded: number;
    }
  | Flagged
  /** Too short to give useful feedback on: no model call, the learner is asked to add more. */
  | { status: "too_short" };

/** Fewer than 4 words ("ok", "I don't know") cannot be scored against a rubric. */
export const MIN_WORDS = 4;
export const wordCount = (text: string) => text.trim().split(/\s+/).filter(Boolean).length;

export interface ScoreRequest {
  challenge: Challenge;
  rubric: Rubric;
  examples: Sample[];
  answer: string;
  lang: Lang;
  previous?: PreviousAttempt;
}

const feedbackText = (fb: Feedback) => [...fb.strengths.map((s) => s.text), fb.suggestion.text].join("\n");

/**
 * Holds model scores to what the answer plainly contains, then makes sure the tip is about the weakest criterion.
 * Every level stays within one step of the evidence, so a plan is never "Good" when the answer has no step.
 * Causes and amounts are easy to see, so an answer that has them is never told it lacks them.
 */
export function checkAgainstAnswer(fb: Feedback, req: ScoreRequest, answer: string): Feedback {
  const scores = { ...fb.scores };
  for (const c of req.rubric.criteria) {
    const { level } = evidenceFor(c, answer, req.challenge.text);
    scores[c.id] = Math.min(level + 1, Math.max(level - 1, scores[c.id]));
    if ((c.kind === "numbers" || c.kind === "understand") && level >= 3) scores[c.id] = Math.max(scores[c.id], 3);
  }
  const target = pickSuggestion(req.rubric, scores, req.previous);
  const current = fb.suggestion.criterionId;
  const prev = req.previous;
  const repeatsFixedTip = !!prev && current === prev.suggestionCriterionId && scores[current] > (prev.levels[current] ?? 4) && current !== target;
  // Keep the model's own words when it already picked the weakest criterion and the learner is not yet "Good" there.
  if (scores[current] === scores[target] && !repeatsFixedTip && scores[current] < 3) return { ...fb, scores };
  const c = req.rubric.criteria.find((x) => x.id === (scores[current] === scores[target] && !repeatsFixedTip ? current : target))!;
  return { ...fb, scores, suggestion: { criterionId: c.id, text: tipFor(c, scores[c.id], req.lang) } };
}

export async function scoreSafely(req: ScoreRequest, deps: Deps = defaultDeps()): Promise<ScoreOutcome> {
  if (wordCount(req.answer) < MIN_WORDS) return { status: "too_short" };
  const { text: answer, found } = redactPii(req.answer);
  const context = { courseId: req.challenge.courseId, templateId: req.challenge.templateId, lang: req.lang };

  const input = await deps.safety.checkText(answer);
  if (input.flagged) {
    addFlag({ ...context, stage: "answer", categories: input.categories, excerpt: answer });
    return { status: "flagged", categories: input.categories };
  }

  // The Level 1 answer comes back from the browser, so it gets the same stripping and screening as the new answer.
  let previous = req.previous;
  if (previous) {
    const prior = redactPii(previous.answer).text;
    const check = await deps.safety.checkText(prior);
    if (check.flagged) {
      addFlag({ ...context, stage: "answer", categories: check.categories, excerpt: prior });
      return { status: "flagged", categories: check.categories };
    }
    previous = { ...previous, answer: prior };
  }

  const scoreInput = { ...req, answer, previous };
  const feedback = checkAgainstAnswer(await deps.provider.scoreAnswer(scoreInput), scoreInput, answer);
  const output = await deps.safety.checkText(feedbackText(feedback));
  if (!output.flagged) {
    // Only model-written praise is checked: the mock's strengths are educator-written rubric lines, not claims about the answer.
    const grounded = deps.provider === mockProvider ? { feedback, dropped: 0 } : groundStrengths(feedback, answer, req.lang);
    return { status: "ok", feedback: grounded.feedback, answer, redacted: found, fallback: false, ungrounded: grounded.dropped };
  }

  // Never show flagged model output. The mock provider uses only educator-written rubric text, so it is a safe stand-in.
  addFlag({ ...context, stage: "feedback", categories: output.categories, excerpt: "" });
  return { status: "ok", feedback: await mockProvider.scoreAnswer(scoreInput), answer, redacted: found, fallback: true, ungrounded: 0 };
}

export async function challengeSafely(template: Template, locale: Locale, lang: Lang, seed: number, deps: Deps = defaultDeps()): Promise<Challenge> {
  const challenge = await deps.provider.generateChallenge(template, locale, lang, seed);
  const check = await deps.safety.checkText(challenge.text);
  if (!check.flagged) return challenge;
  addFlag({ courseId: template.courseId, templateId: template.id, lang, stage: "challenge", categories: check.categories, excerpt: "" });
  // Fall back to the educator-written local details instead of the model's picks.
  return mockProvider.generateChallenge(template, locale, lang, seed);
}

export type TranscribeOutcome = { status: "ok"; text: string; redacted: PiiKind[] } | Flagged;

export async function transcribeSafely(image: Uint8Array, mimeType: string, deps: Deps = defaultDeps()): Promise<TranscribeOutcome> {
  const picture = await deps.safety.checkImage(image);
  if (picture.flagged) {
    addFlag({ stage: "photo", categories: picture.categories, excerpt: "" });
    return { status: "flagged", categories: picture.categories };
  }
  const { text, found } = redactPii(await deps.provider.transcribePhoto(image, mimeType));
  const check = await deps.safety.checkText(text);
  if (check.flagged) {
    addFlag({ stage: "photo", categories: check.categories, excerpt: text });
    return { status: "flagged", categories: check.categories };
  }
  return { status: "ok", text, redacted: found };
}
