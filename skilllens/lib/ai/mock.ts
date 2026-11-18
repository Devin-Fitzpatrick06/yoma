// Mock AI provider: runs with no API keys. Scores each criterion from what the answer actually contains (lib/evidence.ts)
// and writes feedback from the educator's rubric text, quoting the learner's own words.
import { evidenceFor, pickSuggestion, tipFor } from "../evidence";
import { Feedback, type Challenge, type Lang, type Locale, type Template } from "../schema";
import { fillTemplate, slotValues, templateSlots } from "../template";
import type { AIProvider, ScoreInput } from "./provider";

function feedback({ rubric, challenge, answer, lang, previous }: ScoreInput): Feedback {
  const read = rubric.criteria.map((c) => ({ c, ...evidenceFor(c, answer, challenge.text) }));
  const scores = Object.fromEntries(read.map((r) => [r.c.id, r.level]));
  const best = [...read].sort((a, b) => b.level - a.level);
  const strong = best.filter((r) => r.level >= 3).slice(0, 2);
  // "Did well" is never empty: fall back to the learner's strongest criterion.
  const strengths = (strong.length ? strong : best.slice(0, 1)).map((r) => ({
    criterionId: r.c.id,
    text: r.quote && r.level >= 2 ? `${r.c.strength[lang]} “${r.quote}”` : r.c.strength[lang],
  }));
  const weakest = rubric.criteria.find((c) => c.id === pickSuggestion(rubric, scores, previous))!;
  return Feedback.parse({ scores, strengths, suggestion: { criterionId: weakest.id, text: tipFor(weakest, scores[weakest.id], lang) } });
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
    return feedback(input);
  },

  async transcribePhoto() {
    return "[Mock transcription] Amina should buy fewer tomatoes when rain is forecast and sell to regular customers on WhatsApp.";
  },
};
