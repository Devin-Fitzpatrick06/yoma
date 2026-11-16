// Mock AI provider: runs with no API keys. Scores by comparing the answer to the educator-scored examples.
import { Feedback, type Challenge, type Lang, type Locale, type Template } from "../schema";
import { fillTemplate, slotValues, templateSlots } from "../template";
import type { AIProvider, ScoreInput } from "./provider";

const K = 3;

function tokens(text: string): Set<string> {
  return new Set(text.toLowerCase().split(/[^\p{L}\p{N}₦]+/u).filter((w) => w.length > 2));
}

function similarity(a: Set<string>, b: Set<string>): number {
  let shared = 0;
  for (const w of a) if (b.has(w)) shared++;
  const union = a.size + b.size - shared;
  return union === 0 ? 0 : shared / union;
}

// shortcut: whole-answer nearest neighbours, so one strong criterion can lift the others; replaced by the real model in Phase 2.
function score({ rubric, examples, answer }: ScoreInput): Record<string, number> {
  const answerTokens = tokens(answer);
  const ranked = examples
    .map((e) => ({ e, sim: similarity(answerTokens, tokens(e.answer)) }))
    .sort((a, b) => b.sim - a.sim)
    .slice(0, K);
  const scores: Record<string, number> = {};
  for (const { id } of rubric.criteria) {
    const total = ranked.reduce((s, r) => s + r.sim, 0);
    if (total === 0) scores[id] = 1;
    else if (ranked[0].sim === 1) scores[id] = ranked[0].e.scores[id];
    else scores[id] = Math.round(ranked.reduce((s, r) => s + r.sim * r.e.scores[id], 0) / total);
  }
  return scores;
}

function feedback(input: ScoreInput, scores: Record<string, number>): Feedback {
  const lang: Lang = input.lang;
  const criteria = input.rubric.criteria;
  const best = [...criteria].sort((a, b) => scores[b.id] - scores[a.id]);
  const strong = best.filter((c) => scores[c.id] >= 3);
  // "Did well" is never empty: fall back to the learner's strongest criterion.
  const strengths = (strong.length ? strong : best.slice(0, 1)).map((c) => ({ criterionId: c.id, text: c.strength[lang] }));
  const weakest = best[best.length - 1];
  return Feedback.parse({
    scores,
    strengths,
    suggestion: { criterionId: weakest.id, text: weakest.improve[lang] },
  });
}

export const mockProvider: AIProvider = {
  async generateChallenge(template: Template, locale: Locale, lang: Lang, seed: number): Promise<Challenge> {
    const all = slotValues(locale, seed);
    const values = Object.fromEntries(templateSlots(template.text[lang]).map((s) => [s, all[s]]));
    return {
      templateId: template.id,
      courseId: template.courseId,
      rubricId: template.rubricId,
      lang,
      values,
      text: fillTemplate(template.text[lang], values),
    };
  },

  async scoreAnswer(input) {
    return feedback(input, score(input));
  },

  async transcribePhoto() {
    return "[Mock transcription] Amina should buy fewer tomatoes when rain is forecast and sell to regular customers on WhatsApp.";
  },
};
