// Mock AI provider: runs with no API keys. Scores each criterion from what the answer actually contains (lib/evidence.ts)
// and writes feedback from the educator's rubric text, quoting the learner's own words.
import { evidenceFor, pickSuggestion, tipFor } from "../evidence";
import { t } from "../i18n";
import { Feedback, type Challenge, type Lang, type Locale, type Template } from "../schema";
import { fillTemplate, slotValues, templateSlots } from "../template";
import type { AIProvider, ScoreInput } from "./provider";

function feedback({ rubric, challenge, answer, lang, previous }: ScoreInput): Feedback {
  const read = rubric.criteria.map((c) => ({ c, ...evidenceFor(c, answer, challenge.text) }));
  const scores = Object.fromEntries(read.map((r) => [r.c.id, r.level]));
  const best = [...read].sort((a, b) => b.level - a.level);
  const strong = best.filter((r) => r.level >= 3).slice(0, 2);
  // "Did well" is never empty, and never praises what is not there: with nothing "Good" yet, it thanks the effort.
  const strengths = strong.length
    ? strong.map((r) => ({ criterionId: r.c.id, text: r.quote ? `${r.c.strength[lang]} “${r.quote}”` : r.c.strength[lang] }))
    : [{ criterionId: rubric.criteria[0].id, text: t(lang, "effortStrength") }];
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
    // No vision model in simulated mode, so nothing is read from the photo: say so, and let the learner type their notes.
    return "[Mock transcription: simulated mode cannot read photos. Type what your notes say here.]";
  },
};
