import { useMutation } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { IoSparkles } from "react-icons/io5";
import Moment from "react-moment";
import type { CourseQualityAssessment } from "~/api/models/courseQuality";
import { BTN_SECONDARY } from "~/components/Common/buttonStyles";
import { ApiErrors } from "~/components/Status/ApiErrors";
import { DATE_FORMAT_HUMAN } from "~/lib/constants";
import {
  formatElapsed,
  formatRunLine,
  LOCAL_BADGE_TEXT,
  NO_AI_TEXT,
  OUTCOME_BADGES,
  OUTCOME_EXPLANATIONS,
  OUTCOME_LABELS,
  REMOTE_BADGE_TEXT,
} from "./AIUsage.helpers";

// ─────────────────────────────────────────────────────────────────────────────
// Reviewer card "AI check" (Ziqi iteration 5): what the AI said, the run behind it (model, host,
// tokens, cost, time, outcome) and a "Run AI check" button that assesses the course again on the
// configured model. A local model can take minutes, so the pending state counts the seconds.
// ─────────────────────────────────────────────────────────────────────────────

const useElapsedSeconds = (running: boolean): number => {
  const [elapsed, setElapsed] = useState(0);
  const startedAt = useRef<number | null>(null);
  useEffect(() => {
    if (!running) {
      startedAt.current = null;
      return;
    }
    startedAt.current = Date.now();
    setElapsed(0);
    const id = window.setInterval(() => {
      if (startedAt.current != null)
        setElapsed(Math.floor((Date.now() - startedAt.current) / 1000));
    }, 1000);
    return () => window.clearInterval(id);
  }, [running]);
  return elapsed;
};

const AIUsageCheck: React.FC<{
  item: CourseQualityAssessment;
  /** The configured model in words, e.g. "qwen2.5:3b on localhost:11434"; null when unknown. */
  configuredModel?: string | null;
  /** Assesses the opportunity again; leave out to hide the button. */
  run?: (opportunityId: string) => Promise<CourseQualityAssessment>;
  onAssessed?: (assessment: CourseQualityAssessment) => void;
  headingLevel?: "h3" | "h4";
}> = ({ item, configuredModel, run, onAssessed, headingLevel = "h3" }) => {
  const usage = item.aiUsage;
  const Heading = headingLevel;

  const mutation = useMutation({
    mutationFn: (opportunityId: string) => {
      if (!run) throw new Error("No AI check is available here.");
      return run(opportunityId);
    },
    onSuccess: (assessment) => onAssessed?.(assessment),
  });
  const elapsed = useElapsedSeconds(mutation.isPending);
  const answered = usage?.outcome === "Answered";

  return (
    <section
      aria-label="AI check"
      aria-busy={mutation.isPending}
      className="flex flex-col gap-2"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Heading className="font-nunito text-base font-black text-black">
          AI check
        </Heading>
        {usage && (
          <span
            className={`badge h-auto border-none text-xs font-semibold ${
              usage.isLocal
                ? "bg-green-light text-green"
                : "bg-orange-light text-black"
            }`}
          >
            {usage.isLocal ? LOCAL_BADGE_TEXT : REMOTE_BADGE_TEXT}
          </span>
        )}
      </div>

      {/* WHAT THE AI SAID */}
      {item.aiSummary && (
        <div className="bg-gray-light rounded-lg px-3 py-2">
          <p className="text-xs font-semibold">
            {answered ? "The AI's summary" : "Evidence summary"}
          </p>
          <p className="text-gray-dark text-sm break-words whitespace-pre-line">
            {item.aiSummary}
          </p>
        </div>
      )}

      {/* THE RUN */}
      {usage ? (
        <div className="flex flex-col gap-1.5">
          <p className="text-sm break-words text-black">
            {formatRunLine(usage)}
          </p>
          <div className="flex flex-wrap items-start gap-2">
            <span
              className={`badge h-auto shrink-0 border-none text-xs font-semibold ${OUTCOME_BADGES[usage.outcome] ?? "bg-gray-light text-black"}`}
            >
              {OUTCOME_LABELS[usage.outcome] ?? usage.outcome}
            </span>
            <p className="text-gray-dark min-w-0 flex-1 text-xs break-words">
              {OUTCOME_EXPLANATIONS[usage.outcome] ?? ""}
            </p>
          </div>
          {usage.dateRun && (
            <p className="text-gray-dark text-xs">
              Ran{" "}
              <Moment format={DATE_FORMAT_HUMAN} utc={true}>
                {usage.dateRun}
              </Moment>
            </p>
          )}
        </div>
      ) : item.aiUsed && usage === undefined ? (
        <p className="text-sm text-black">
          An AI ran{item.aiModel ? ` (${item.aiModel})` : ""} before usage was
          recorded, so its tokens and cost are not known.
        </p>
      ) : (
        <div className="flex flex-col gap-0.5">
          <p className="text-sm text-black">{NO_AI_TEXT}</p>
          <p className="text-gray-dark text-xs">
            {item.aiModel
              ? "The course page was read with fixed rules for its dates, cost and format."
              : "No course page could be read, so confidence is capped."}
          </p>
        </div>
      )}

      {/* RUN AI CHECK */}
      {run && (
        <div className="flex flex-col gap-2 pt-1">
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              className={`${BTN_SECONDARY} gap-2`}
              disabled={mutation.isPending}
              onClick={() => mutation.mutate(item.opportunityId)}
            >
              {mutation.isPending ? (
                <span
                  aria-hidden="true"
                  className="loading loading-spinner loading-xs"
                />
              ) : (
                <IoSparkles aria-hidden="true" className="h-4 w-4" />
              )}
              {mutation.isPending ? "Running AI check" : "Run AI check"}
            </button>
            {!mutation.isPending && (
              <span className="text-gray-dark text-xs">
                Assesses this course again
                {configuredModel ? ` on ${configuredModel}` : ""}.
              </span>
            )}
          </div>
          <p
            role="status"
            aria-live="polite"
            className={`text-sm ${mutation.isPending ? "bg-blue-light rounded-lg px-3 py-2 text-black" : "sr-only"}`}
          >
            {mutation.isPending
              ? `Running on ${configuredModel ?? "the configured model"}: ${formatElapsed(elapsed)} so far. A local model can take a few minutes; you can keep this page open.`
              : mutation.isSuccess
                ? "AI check finished. The card shows the new result."
                : ""}
          </p>
          {mutation.isError && (
            <div
              role="alert"
              className="bg-orange-light flex flex-col gap-1 rounded-lg px-3 py-2 text-sm text-black"
            >
              <span className="font-semibold">
                The AI check did not finish.
              </span>
              <ApiErrors error={mutation.error} />
            </div>
          )}
        </div>
      )}
    </section>
  );
};

export default AIUsageCheck;
