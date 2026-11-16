import { t } from "../lib/i18n";
import type { Lang } from "../lib/schema";
import { Icon } from "./Icon";

export interface LearnerFeedback {
  strengths: { criterionId: string; text: string }[];
  suggestion: { criterionId: string; text: string };
}

export function FeedbackPanels({ feedback, lang }: { feedback: LearnerFeedback; lang: Lang }) {
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      <section aria-labelledby="did-well" className="card-brand">
        <h3 id="did-well" className="flex items-center gap-2 text-xl text-brand"><Icon name="check" />{t(lang, "didWell")}</h3>
        <ul className="mt-2 space-y-2">
          {feedback.strengths.map((s) => (
            <li key={s.criterionId}>{s.text}</li>
          ))}
        </ul>
      </section>
      <section aria-labelledby="to-improve" className="card-yellow">
        <h3 id="to-improve" className="flex items-center gap-2 text-xl text-brand"><Icon name="arrow" />{t(lang, "toImprove")}</h3>
        <p className="mt-2">{feedback.suggestion.text}</p>
      </section>
    </div>
  );
}
