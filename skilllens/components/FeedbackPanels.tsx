import { t, type MessageKey } from "../lib/i18n";
import type { Lang } from "../lib/schema";
import { Icon } from "./Icon";

export interface RubricRow {
  id: string;
  name: string;
  level: number;
  /** What this level looks like, in words. */
  now: string;
  /** What the next level looks like; null at the top level. */
  next: string | null;
  /** Level on the first try (Level 2 only). */
  before: number | null;
}

export interface LearnerFeedback {
  strengths: { criterionId: string; text: string }[];
  suggestion: { criterionId: string; text: string };
  levels: Record<string, number>;
  rubric: RubricRow[];
}

const levelName = (lang: Lang, n: number) => t(lang, `level${n}` as MessageKey);

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

/** One row per rubric criterion: the level in words on a 4-step bar, what we saw, and what the next level needs. */
export function RubricPanel({ rows, lang }: { rows: RubricRow[]; lang: Lang }) {
  const second = rows.some((r) => r.before !== null);
  const improved = rows.filter((r) => r.before !== null && r.level > r.before);
  return (
    <section aria-labelledby="rubric" className="space-y-3 rounded-2xl border-2 border-brand-soft p-4">
      <div>
        <h3 id="rubric" className="text-xl text-brand">{t(lang, "rubricTitle")}</h3>
        <p className="text-sm">{t(lang, "rubricIntro")}</p>
      </div>
      {second && (
        <p role="status" className={`flex gap-2 rounded-xl p-3 text-sm font-medium ${improved.length ? "bg-brand-tint text-brand" : "bg-yellow/15"}`}>
          <Icon name={improved.length ? "star" : "info"} className="size-5 shrink-0" />
          {improved.length ? `${t(lang, "progressSome")}: ${improved.map((r) => r.name).join(", ")}.` : t(lang, "progressNone")}
        </p>
      )}
      <ul className="grid gap-3 lg:grid-cols-2">
        {rows.map((r) => (
          <li key={r.id} className="card space-y-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h4 className="font-heading text-lg text-brand">{r.name}</h4>
              <span className={`rounded-full px-3 py-0.5 text-sm font-medium ${r.level >= 3 ? "bg-brand text-white" : "bg-yellow text-brand"}`}>{levelName(lang, r.level)}</span>
            </div>
            <div className="flex gap-1" role="img" aria-label={`${r.name}: ${levelName(lang, r.level)}`}>
              {[1, 2, 3, 4].map((n) => (
                <span key={n} className={`h-2 flex-1 rounded-full ${n <= r.level ? "bg-brand" : "bg-brand-soft/40"}`} />
              ))}
            </div>
            {r.before !== null && (
              <p className="text-sm font-medium text-brand">
                {r.level > r.before ? `↑ ${t(lang, "improvedFrom")} ${levelName(lang, r.before)}` : t(lang, "sameAs")}
              </p>
            )}
            <p className="text-sm"><span className="font-medium">{t(lang, "nowLabel")}:</span> {r.now}</p>
            <p className="text-sm">
              {r.next ? <><span className="font-medium">{t(lang, "nextLabel")}:</span> {r.next}</> : t(lang, "topLevel")}
            </p>
          </li>
        ))}
      </ul>
    </section>
  );
}
