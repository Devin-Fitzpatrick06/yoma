import { useMemo, useState } from "react";
import { IoDownloadOutline } from "react-icons/io5";
import Moment from "react-moment";
import type {
  LinkHealthReport,
  LinkHealthStatus,
} from "~/api/models/courseQuality";
import { BTN_PRIMARY } from "~/components/Common/buttonStyles";
import { DATE_FORMAT_HUMAN } from "~/lib/constants";
import { formatDuration } from "./AIUsage.helpers";
import {
  LINK_HEALTH_BADGES,
  LINK_HEALTH_EXPLANATIONS,
  LINK_HEALTH_LABELS,
  LINK_HEALTH_STATUSES,
  linkHealthFileName,
  linkHealthToCsv,
} from "./LinkHealth.helpers";

// ─────────────────────────────────────────────────────────────────────────────
// Link health results (Ziqi iteration 5): counts per status that filter the list, a row per link
// with a status chip (colour and text), and a client-side CSV download.
// ─────────────────────────────────────────────────────────────────────────────

const ALL = "All";
const PAGE = 100;

export const LinkHealthChip: React.FC<{ status: LinkHealthStatus }> = ({
  status,
}) => (
  <span
    className={`badge h-auto shrink-0 border-none text-xs font-semibold whitespace-nowrap ${LINK_HEALTH_BADGES[status] ?? "bg-gray-light text-black"}`}
  >
    {LINK_HEALTH_LABELS[status] ?? status}
  </span>
);

const downloadCsv = (report: LinkHealthReport) => {
  const blob = new Blob([linkHealthToCsv(report)], {
    type: "text/csv;charset=utf-8",
  });
  const href = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = href;
  anchor.download = linkHealthFileName(report.platform, new Date());
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(href), 0);
};

const LinkHealthResults: React.FC<{ report: LinkHealthReport }> = ({
  report,
}) => {
  const [filter, setFilter] = useState<LinkHealthStatus | typeof ALL>(ALL);
  const [shown, setShown] = useState(PAGE);

  const visible = useMemo(
    () =>
      filter === ALL
        ? report.results
        : report.results.filter((result) => result.status === filter),
    [report.results, filter],
  );
  const present = LINK_HEALTH_STATUSES.filter(
    (status) => (report.counts[status] ?? 0) > 0,
  );

  return (
    <section
      aria-labelledby="link-health-results-heading"
      className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm md:p-5"
    >
      <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div className="flex min-w-0 flex-col gap-0.5">
          <h2
            id="link-health-results-heading"
            className="font-nunito text-lg font-black text-black"
          >
            {report.checked === 1
              ? "1 link checked"
              : `${report.checked} links checked`}
            {report.platform ? ` · ${report.platform}` : ""}
          </h2>
          <p className="text-gray-dark text-xs">
            <Moment format={DATE_FORMAT_HUMAN} utc={true}>
              {report.dateChecked}
            </Moment>
          </p>
        </div>
        <button
          type="button"
          className={`${BTN_PRIMARY} gap-2`}
          onClick={() => downloadCsv(report)}
        >
          <IoDownloadOutline aria-hidden="true" className="h-4 w-4" />
          Download CSV
        </button>
      </div>

      {/* COUNTS, which filter the list */}
      <div
        role="group"
        aria-label="Show links by status"
        className="flex flex-wrap gap-2"
      >
        {[ALL as typeof ALL | LinkHealthStatus, ...present].map((status) => {
          const selected = filter === status;
          const count =
            status === ALL ? report.checked : (report.counts[status] ?? 0);
          return (
            <button
              key={status}
              type="button"
              aria-pressed={selected}
              onClick={() => {
                setFilter(status);
                setShown(PAGE);
              }}
              className={`inline-flex min-h-8 items-center gap-1.5 rounded-full border px-3 text-xs font-semibold hover:brightness-95 ${
                selected
                  ? "border-black bg-black text-white"
                  : status === ALL
                    ? "border-gray bg-white text-black"
                    : `border-transparent ${LINK_HEALTH_BADGES[status]}`
              }`}
            >
              {status === ALL ? "All" : LINK_HEALTH_LABELS[status]}
              <span className="font-nunito font-black">{count}</span>
            </button>
          );
        })}
      </div>

      {filter !== ALL && (
        <p className="text-gray-dark text-sm">
          {LINK_HEALTH_EXPLANATIONS[filter]}
        </p>
      )}

      {/* THE LINKS */}
      <ul
        aria-label={`Links: ${filter === ALL ? "all" : LINK_HEALTH_LABELS[filter]}`}
        className="divide-gray-light flex flex-col divide-y"
      >
        {visible.slice(0, shown).map((result, index) => (
          <li
            key={`${result.url}-${index}`}
            className="flex flex-col gap-1 py-2.5 first:pt-0 last:pb-0 md:flex-row md:items-start md:gap-3"
          >
            <div className="md:w-36 md:shrink-0">
              <LinkHealthChip status={result.status} />
            </div>
            <div className="flex min-w-0 flex-1 flex-col gap-0.5">
              <span className="text-sm break-all text-black">{result.url}</span>
              {result.finalUrl && result.finalUrl !== result.url && (
                <span className="text-gray-dark text-xs break-all">
                  Now at: {result.finalUrl}
                </span>
              )}
              <span className="text-gray-dark text-xs break-words">
                {result.reason}
              </span>
            </div>
            <span className="text-gray-dark text-xs whitespace-nowrap md:w-28 md:text-right">
              {[
                result.httpStatus != null ? `HTTP ${result.httpStatus}` : null,
                result.durationMs != null
                  ? formatDuration(result.durationMs)
                  : null,
              ]
                .filter(Boolean)
                .join(" · ")}
            </span>
          </li>
        ))}
      </ul>
      {visible.length > shown && (
        <button
          type="button"
          className="text-theme self-start text-sm font-semibold hover:underline"
          onClick={() => setShown((value) => value + PAGE)}
        >
          Show {Math.min(PAGE, visible.length - shown)} more of{" "}
          {visible.length - shown}
        </button>
      )}
    </section>
  );
};

export default LinkHealthResults;
