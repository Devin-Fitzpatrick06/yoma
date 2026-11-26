// The end screen's handoff item: the Skill Credential, recorded on the learner's YoID.
// - With a sample learner (the BBS x GenU shared mock data, Halima): a MOCKED YoID write through /api/yoid/credential,
//   only after a server-scored pass and the learner's tap (consent). It returns cred-002 and the updated YoID thread.
// - Without one: a design preview of the same flow, nothing sent (INTEGRATION.md section 8).
// Pass rule (proposed): the Level 2 answer is "Good" or "Strong" on every rubric criterion (lib/credential.ts).
import { useState } from "react";
import { PASS_LEVEL, passed } from "../lib/credential";
import { t, type MessageKey } from "../lib/i18n";
import type { Lang, YomaSkill } from "../lib/schema";
import { credential, shared, skillLabel } from "../lib/shared";
import type { HandoffRecord, IssuedCredential } from "../lib/yoid";
import type { RubricRow } from "./FeedbackPanels";
import { Icon } from "./Icon";

const STEPS: MessageKey[] = ["credStepSent", "credStepChecked", "credStepWallet"];

export interface SampleLearner {
  yoidId: string;
  displayName: string;
}

interface Props {
  lang: Lang;
  rows: RubricRow[];
  skills: YomaSkill[];
  lesson: string | null;
  date: string;
  /** Design preview only: the learner tapped "Add to my Yoma profile". */
  sent: boolean;
  learner: SampleLearner | null;
  passId: string | null;
  onRecorded: (thread: HandoffRecord[]) => void;
}

export function CredentialHandoff({ lang, rows, skills, lesson, date, sent, learner, passId, onRecorded }: Props) {
  const [status, setStatus] = useState<"ready" | "sending" | "error">("ready");
  const [issued, setIssued] = useState<IssuedCredential | null>(null);
  const ok = passed(rows);
  const live = !!learner && !!passId; // mocked YoID write available
  const done = live ? !!issued : sent;
  // With the sample learner, the card shows cred-002 exactly as the shared data has it, before and after the write.
  const sharedCred = learner ? credential(shared.skillChallenge.issues) : undefined;
  const card = issued ?? (live && sharedCred ? { ...sharedCred, skills: sharedCred.skills.map(skillLabel), issuedOn: sharedCred.issuedOn ?? date } : null);

  async function record() {
    setStatus("sending");
    try {
      const res = await fetch("/api/yoid/credential", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ yoidId: learner!.yoidId, passId }),
      });
      if (!res.ok) throw new Error(String(res.status));
      const out = (await res.json()) as { credential: IssuedCredential; thread: HandoffRecord[] };
      setIssued(out.credential);
      setStatus("ready");
      onRecorded(out.thread);
    } catch {
      setStatus("error");
    }
  }

  const title: MessageKey = !ok ? "credNotYetTitle" : live ? (issued ? "credYoidDoneTitle" : "credYoidReadyTitle") : sent ? "credPassedTitle" : "credReadyTitle";
  return (
    <section aria-labelledby="credential" className="space-y-3 rounded-2xl border-2 border-dashed border-brand-soft p-4">
      <p className="inline-flex items-center gap-2 rounded-full bg-accent px-3 py-1 text-sm font-semibold text-brand">
        <Icon name="info" className="size-4" />{t(lang, live ? "credYoidMock" : "credPreview")}
      </p>
      <h2 id="credential" className="text-xl text-brand">{t(lang, title)}</h2>

      {ok ? (
        <>
          {/* The credential as it looks in the YoID wallet. */}
          <div className="rounded-2xl bg-brand p-4 text-paper shadow-sm">
            <p className="flex items-center gap-2 text-sm"><Icon name="cap" className="size-5" />{card ? card.issuer : t(lang, "credIssuer")}</p>
            <p className="mt-2 font-heading text-2xl leading-tight">{card?.title ?? t(lang, "credName")}</p>
            {!card && lesson && <p className="mt-1 text-sm">{lesson}</p>}
            <ul className="mt-3 flex flex-wrap gap-2">
              {(card ? card.skills.map((name) => ({ id: name, name })) : skills).map((s) => (
                <li key={s.id} className="rounded-full bg-paper px-3 py-1 text-sm font-semibold text-brand">{s.name}</li>
              ))}
            </ul>
            <p className="mt-3 text-sm">{t(lang, "credAssessed")} · {card?.issuedOn ?? date}</p>
            {card && <p className="mt-1 text-sm">{t(lang, "credIdLabel")} {card.id} · YoID {card.yoidId}</p>}
          </div>

          <ol className="grid gap-2 sm:grid-cols-3" aria-label={t(lang, "credJourney")}>
            {STEPS.map((key, i) => {
              const reached = live ? !!issued : sent && i === 0;
              return (
                <li key={key} className={`flex items-center gap-2 rounded-xl p-3 text-sm ${reached ? "bg-brand-tint font-semibold text-brand" : "bg-surface"}`}>
                  <span className={`grid size-7 shrink-0 place-items-center rounded-full ${reached ? "bg-brand text-paper" : "border-2 border-brand-soft text-brand"}`}>
                    {reached && live ? <Icon name="check" className="size-5" /> : i + 1}
                  </span>
                  {t(lang, key)}
                  {reached && <span className="sr-only">: {t(lang, "credStepDone")}</span>}
                </li>
              );
            })}
          </ol>

          {live ? (
            issued ? (
              <p role="status" className="card-brand flex gap-2 text-sm font-semibold text-brand"><Icon name="arrow" className="size-5 shrink-0" />{t(lang, "credYoidNext")}</p>
            ) : (
              <>
                <p className="text-sm">{t(lang, "credYoidConsent")}</p>
                <button type="button" className="btn-accent" disabled={status === "sending"} onClick={record}>
                  <Icon name="check" /> {t(lang, status === "sending" ? "credYoidSending" : "credYoidAdd")}
                </button>
                {status === "error" && <p role="alert" className="text-sm text-coral">{t(lang, "credYoidError")}</p>}
              </>
            )
          ) : (
            <p className="text-sm">{t(lang, done ? "credTiming" : "credConsent")}</p>
          )}
          <p className="card text-sm">{t(lang, "credWhat")}</p>
          {!live && (
            <>
              <button type="button" className="btn-accent" disabled aria-describedby="credential-disabled">
                <Icon name="external" /> {t(lang, "credOpen")}
              </button>
              <p id="credential-disabled" className="text-center text-sm">{t(lang, "credPreviewButton")}</p>
            </>
          )}
        </>
      ) : (
        <>
          <p>{t(lang, "credNotYetBody")}</p>
          <ul className="space-y-1">
            {rows.filter((r) => r.level < PASS_LEVEL).map((r) => (
              <li key={r.id} className="flex items-center gap-2"><Icon name="arrow" className="size-5 shrink-0 text-brand" />{r.name}</li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
