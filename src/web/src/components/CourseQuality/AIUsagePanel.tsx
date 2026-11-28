import Link from "next/link";
import { useId } from "react";
import Moment from "react-moment";
import type { CourseQualityAIUsageSummary } from "~/api/models/courseQuality";
import { BTN_SECONDARY } from "~/components/Common/buttonStyles";
import { DATE_FORMAT_HUMAN } from "~/lib/constants";
import {
  CONFIGURE_AI_STEPS,
  COST_NOT_CONFIGURED_TEXT,
  describeModel,
  formatDuration,
  formatRunCost,
  formatTokens,
  formatTotalCost,
  LOCAL_BADGE_TEXT,
  OUTCOME_BADGES,
  OUTCOME_LABELS,
  PROVIDER_LABELS,
  REMOTE_BADGE_TEXT,
} from "./AIUsage.helpers";

// ─────────────────────────────────────────────────────────────────────────────
// Admin "AI usage" (Ziqi iteration 5), on the reviewer queue: which model the checks run on,
// whether it is local (free) or remote (billed), and what the AI runs have used and cost.
// ─────────────────────────────────────────────────────────────────────────────

const Total: React.FC<{ label: string; value: string }> = ({
  label,
  value,
}) => (
  <div className="bg-gray-light flex flex-col rounded-lg px-3 py-2">
    <dt className="text-gray-dark text-xs">{label}</dt>
    <dd className="font-nunito text-base font-black break-words text-black">
      {value}
    </dd>
  </div>
);

const AIUsagePanel: React.FC<{
  summary: CourseQualityAIUsageSummary | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry?: () => void;
}> = ({ summary, isLoading, isError, onRetry }) => {
  const id = useId();
  // from the API's totals, so runs older than the recent list count too
  const unpricedRuns = summary?.unpricedRuns ?? 0;

  return (
    <section
      aria-labelledby={`${id}-heading`}
      className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm md:p-5"
    >
      <div className="flex flex-col gap-0.5">
        <h2
          id={`${id}-heading`}
          className="font-nunito text-lg font-black text-black"
        >
          AI usage
        </h2>
        <p className="text-gray-dark text-sm">
          The model behind the AI check, and the tokens, cost and time its runs
          used.
        </p>
      </div>

      {isError && !summary ? (
        <div role="alert" className="flex items-center gap-3 text-sm">
          AI usage could not be loaded.
          {onRetry && (
            <button type="button" className={BTN_SECONDARY} onClick={onRetry}>
              Try again
            </button>
          )}
        </div>
      ) : isLoading || !summary ? (
        <p className="text-gray-dark text-sm">Loading AI usage...</p>
      ) : (
        <>
          {/* THE CONFIGURED MODEL */}
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="font-semibold text-black">
              {PROVIDER_LABELS[summary.provider] ?? summary.provider}
            </span>
            {summary.configured && (
              <>
                <span aria-hidden="true" className="text-gray-dark">
                  ·
                </span>
                <span className="min-w-0 break-words text-black">
                  {describeModel(
                    summary.provider,
                    summary.model,
                    summary.endpointHost,
                  )}
                </span>
                <span
                  className={`badge h-auto border-none text-xs font-semibold ${
                    summary.isLocal
                      ? "bg-green-light text-green"
                      : "bg-orange-light text-black"
                  }`}
                >
                  {summary.isLocal ? LOCAL_BADGE_TEXT : REMOTE_BADGE_TEXT}
                </span>
              </>
            )}
            <span
              className={`badge h-auto border-none text-xs font-semibold ${
                summary.configured
                  ? "bg-blue-light text-black"
                  : "bg-yellow-tint text-black"
              }`}
            >
              {summary.configured ? "Configured" : "Not configured"}
            </span>
          </div>

          {!summary.configured && (
            <div
              role="note"
              className="bg-yellow-tint flex flex-col gap-2 rounded-lg px-3 py-2 text-sm text-black"
            >
              <p>
                No AI model is configured, so every check uses the rule-based
                reader. To run a free local model, set these in the API
                configuration and restart it:
              </p>
              <dl className="grid grid-cols-1 gap-x-3 gap-y-1 sm:grid-cols-[auto_1fr]">
                {CONFIGURE_AI_STEPS.map((step) => (
                  <div key={step.setting} className="contents">
                    <dt>
                      <code className="text-xs break-all">{step.setting}</code>
                    </dt>
                    <dd className="text-gray-dark mb-1 text-xs break-words sm:mb-0">
                      {step.example}
                    </dd>
                  </div>
                ))}
              </dl>
              <p className="text-gray-dark text-xs">
                For Anthropic, set <code>AI:Provider</code> to Anthropic and its
                API key in the environment. Remote models need a price under{" "}
                <code className="break-all">AI:Prices:&lt;model&gt;</code> to
                show a cost.
              </p>
            </div>
          )}

          {/* TOTALS */}
          <dl className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
            <Total label="Runs" value={formatTokens(summary.runs)} />
            <Total label="Answered" value={formatTokens(summary.answered)} />
            <Total
              label="Tokens in"
              value={formatTokens(summary.inputTokens)}
            />
            <Total
              label="Tokens out"
              value={formatTokens(summary.outputTokens)}
            />
            <Total label="Total cost" value={formatTotalCost(summary)} />
            <Total
              label="Average time"
              value={formatDuration(summary.averageDurationMs)}
            />
          </dl>
          {unpricedRuns > 0 && (
            <p className="text-gray-dark text-xs">
              Total cost counts only runs with a configured price;{" "}
              {unpricedRuns} {unpricedRuns === 1 ? "run" : "runs"} in all (older
              ones included) {unpricedRuns === 1 ? "shows" : "show"} &ldquo;
              {COST_NOT_CONFIGURED_TEXT}&rdquo;.
            </p>
          )}

          {/* RECENT RUNS */}
          {summary.recent.length === 0 ? (
            <p className="bg-gray-light text-gray-dark rounded-lg px-3 py-3 text-sm">
              No AI runs yet. Use &ldquo;Run AI check&rdquo; on a course in
              reviewer mode to run one.
            </p>
          ) : (
            <div
              role="region"
              aria-labelledby={`${id}-recent`}
              tabIndex={0}
              className="focus-visible:outline-theme -mx-1 overflow-x-auto px-1"
            >
              <table className="w-full min-w-[640px] text-left text-sm">
                <caption
                  id={`${id}-recent`}
                  className="text-gray-dark pb-2 text-left text-xs font-semibold"
                >
                  Recent runs, newest first
                </caption>
                <thead className="text-gray-dark text-xs">
                  <tr className="border-gray border-b">
                    <th scope="col" className="py-1.5 pr-3 font-semibold">
                      Course
                    </th>
                    <th scope="col" className="py-1.5 pr-3 font-semibold">
                      Model
                    </th>
                    <th scope="col" className="py-1.5 pr-3 font-semibold">
                      Tokens in / out
                    </th>
                    <th scope="col" className="py-1.5 pr-3 font-semibold">
                      Cost
                    </th>
                    <th scope="col" className="py-1.5 pr-3 font-semibold">
                      Time
                    </th>
                    <th scope="col" className="py-1.5 pr-3 font-semibold">
                      Outcome
                    </th>
                    <th scope="col" className="py-1.5 font-semibold">
                      Date
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-gray-light divide-y">
                  {summary.recent.map((run) => (
                    <tr key={run.assessmentId} className="align-top">
                      <td className="max-w-[16rem] py-2 pr-3">
                        <Link
                          href={`/opportunities/${run.opportunityId}`}
                          className="text-theme font-semibold break-words hover:underline"
                        >
                          {run.opportunityTitle}
                        </Link>
                      </td>
                      <td className="py-2 pr-3 break-words">
                        {run.usage.model}
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap">
                        {formatTokens(run.usage.inputTokens)} /{" "}
                        {formatTokens(run.usage.outputTokens)}
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap">
                        {formatRunCost(run.usage)}
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap">
                        {formatDuration(run.usage.durationMs)}
                      </td>
                      <td className="py-2 pr-3">
                        <span
                          className={`badge h-auto border-none text-xs font-semibold ${OUTCOME_BADGES[run.usage.outcome] ?? "bg-gray-light text-black"}`}
                        >
                          {OUTCOME_LABELS[run.usage.outcome] ??
                            run.usage.outcome}
                        </span>
                      </td>
                      <td className="text-gray-dark py-2 whitespace-nowrap">
                        <Moment format={DATE_FORMAT_HUMAN} utc={true}>
                          {run.usage.dateRun}
                        </Moment>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </section>
  );
};

export default AIUsagePanel;
