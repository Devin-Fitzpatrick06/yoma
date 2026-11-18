// Guards against praise for things the learner never wrote: a "Did well" item must share at least one content word
// with the answer. Small models sometimes credit ideas from the challenge itself (scenario E2 in npm run scenarios).
// shortcut: word overlap, so a strength that paraphrases with entirely different words is dropped too, and the learner
// gets the effort-only strength; a semantic check (embeddings, or the model asked to quote) would keep more of those.
import { t } from "../i18n";
import type { Feedback, Lang } from "../schema";

const STOP = new Set(
  "that this with your have they what when should would could about them their there from which then also just very much like more make some been were will into than only even well good your you she her his him the and for are but not can did does done dey wey go".split(" "),
);

/** Lower-case content words, crudely stemmed so "baskets" matches "basket" and "spending" matches "spend". */
function stems(text: string): Set<string> {
  return new Set(
    text
      .toLowerCase()
      .split(/[^\p{L}\p{N}]+/u)
      .filter((w) => w.length >= 4 && !STOP.has(w))
      .map((w) => w.replace(/(ing|ed|es|s)$/, "")),
  );
}

export function groundStrengths(feedback: Feedback, answer: string, lang: Lang): { feedback: Feedback; dropped: number } {
  const said = stems(answer);
  const kept = feedback.strengths.filter((s) => [...stems(s.text)].some((w) => said.has(w)));
  const dropped = feedback.strengths.length - kept.length;
  if (dropped === 0) return { feedback, dropped };
  // "Did well" is never empty: fall back to honest praise for the attempt, tied to the criterion the model chose first.
  const strengths = kept.length ? kept : [{ criterionId: feedback.strengths[0].criterionId, text: t(lang, "effortStrength") }];
  return { feedback: { ...feedback, strengths }, dropped };
}
