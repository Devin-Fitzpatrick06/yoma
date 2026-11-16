import Link from "next/link";
import Moment from "react-moment";
import { IoOpenOutline } from "react-icons/io5";
import type {
  CourseQualityCriterion,
  CourseQualityCriterionResult,
  CourseQualityStatus,
  PartnerFixNote,
} from "~/api/models/courseQuality";
import { DATE_FORMAT_HUMAN } from "~/lib/constants";

// ─────────────────────────────────────────────────────────────────────────────
// A partner's fix note: why a course is held back and the steps to improve it. Shown on the
// organisation's Course Quality page, and as a preview in the reviewer mode.
// ─────────────────────────────────────────────────────────────────────────────

const CRITERION_LABELS: Record<CourseQualityCriterion, string> = {
  ListingLive: "Listing status",
  LinkWorks: "Course link",
  DatesAccurate: "Course dates",
  DataComplete: "Course details",
  FormatIdentified: "Course format",
};

const STATUS_LABELS: Record<CourseQualityStatus, string> = {
  Verified: "Verified",
  NeedsReview: "Being reviewed",
  NotVerified: "Not yet verified",
};

export const hasHardGateIssue = (note: PartnerFixNote) =>
  note.issues.some((issue) => issue.hardGateFailed);

// highest priority first: the issues that keep a course from being verified at any score
const sortIssues = (issues: CourseQualityCriterionResult[]) =>
  [...issues].sort(
    (a, b) => Number(b.hardGateFailed) - Number(a.hardGateFailed),
  );

const FixNoteCard: React.FC<{
  note: PartnerFixNote;
  /** Where the title links. Plain text when omitted. */
  titleHref?: string;
  /** "See as learner": the public opportunity page. */
  learnerHref?: string;
  className?: string;
  /** Extra content inside the card, above its footer, e.g. what learners said. */
  children?: React.ReactNode;
}> = ({ note, titleHref, learnerHref, className = "", children }) => {
  const issues = sortIssues(note.issues);
  const steps = issues.filter((issue) => issue.recommendation);

  return (
    <article
      className={`flex flex-col gap-4 rounded-lg bg-white p-4 shadow-sm md:p-6 ${className}`}
    >
      <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
        {titleHref ? (
          <Link
            href={titleHref}
            className="text-theme text-base font-semibold break-words hover:underline"
          >
            {note.opportunityTitle}
          </Link>
        ) : (
          <span className="text-base font-semibold break-words">
            {note.opportunityTitle}
          </span>
        )}
        <div className="flex shrink-0 flex-wrap gap-2">
          {hasHardGateIssue(note) && (
            <span className="badge bg-orange-light text-orange border-none text-xs font-semibold">
              Fix first
            </span>
          )}
          <span className="badge bg-gray-light text-gray-dark border-none text-xs font-semibold">
            {STATUS_LABELS[note.status]}
          </span>
        </div>
      </div>

      {/* WHY */}
      <div className="flex flex-col gap-1">
        <h4 className="text-sm font-semibold">Why it is held back</h4>
        <p className="text-gray-dark text-sm break-words whitespace-pre-line">
          {note.summary}
        </p>
        {issues.length > 0 && (
          <ul className="mt-1 flex flex-col gap-1">
            {issues.map((issue) => (
              <li
                key={issue.criterion}
                className="text-gray-dark text-sm break-words"
              >
                <span className="font-semibold">
                  {CRITERION_LABELS[issue.criterion] ?? issue.criterion}:
                </span>{" "}
                {issue.reason}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* STEPS */}
      {steps.length > 0 && (
        <div className="flex flex-col gap-1">
          <h4 className="text-sm font-semibold">
            Steps to improve its visibility
          </h4>
          <ol className="text-gray-dark ml-5 flex list-decimal flex-col gap-1 text-sm">
            {steps.map((issue) => (
              <li key={issue.criterion} className="break-words">
                {issue.recommendation}
              </li>
            ))}
          </ol>
        </div>
      )}

      {children}

      <div className="text-gray-dark flex flex-wrap items-center justify-between gap-2 text-xs">
        <span>
          Last checked{" "}
          <Moment format={DATE_FORMAT_HUMAN} utc={true}>
            {note.dateAssessed}
          </Moment>
        </span>
        {learnerHref && (
          <Link
            href={learnerHref}
            target="_blank"
            rel="noopener noreferrer"
            className="text-gray-dark inline-flex items-center gap-1 font-semibold hover:underline"
          >
            See as learner
            <IoOpenOutline aria-hidden="true" className="h-3.5 w-3.5" />
            <span className="sr-only">(opens in a new tab)</span>
          </Link>
        )}
      </div>
    </article>
  );
};

export default FixNoteCard;
