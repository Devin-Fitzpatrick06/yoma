import type { CourseGradeResult } from "~/api/models/courseQuality";
import {
  GRADE_FACTOR_LABELS,
  isWaitingForReviewCap,
} from "~/lib/courseGrading";
import GradeChip from "./GradeChip";

// ─────────────────────────────────────────────────────────────────────────────
// Why a course has its grade: the weighted score, its percentile, a bar per factor (score out
// of 100, its weight, and whether it had data or fell back to neutral) and the caps that held
// the grade down. Purple bars, never green: green marks verification only.
// ─────────────────────────────────────────────────────────────────────────────

const formatWeight = (weight: number) =>
  `${Math.round(weight * 10) / 10}%`.replace(".0%", "%");

export const WAITING_FOR_REVIEW_TEXT = "Not graded yet — waiting for review";

const GradeBreakdown: React.FC<{
  grade: CourseGradeResult | null;
  /** The course is waiting for review: an ungraded or missing grade is explained by that. */
  waitingForReview?: boolean;
}> = ({ grade, waitingForReview = false }) => {
  if (!grade)
    return (
      <p className="bg-gray-light text-gray-dark rounded-lg px-3 py-2 text-sm">
        {waitingForReview
          ? `${WAITING_FOR_REVIEW_TEXT}. A grade follows once a reviewer confirms it and grading runs.`
          : "Not graded yet. A grade follows the next grading run."}
      </p>
    );

  const factors = [...grade.factors].sort((a, b) => b.weight - a.weight);
  const ungraded = grade.grade === "Ungraded";
  const waiting =
    ungraded && (isWaitingForReviewCap(grade.caps) || waitingForReview);

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        {ungraded ? (
          <span className="badge bg-gray-light text-gray-dark h-auto border-none text-xs font-semibold">
            {waiting ? WAITING_FOR_REVIEW_TEXT : "Not graded"}
          </span>
        ) : (
          <GradeChip grade={grade.grade} label={grade.gradeLabel} size="sm" />
        )}
        <span className="text-sm">
          <span className="font-nunito font-black">{grade.score}</span>
          <span className="text-gray-dark">
            {" "}
            / 100 weighted score{ungraded ? ", if it were graded" : ""}
          </span>
        </span>
        {!ungraded && (
          <span className="text-gray-dark text-sm">
            At or above {grade.percentile}% of graded courses
          </span>
        )}
      </div>

      <ul className="flex flex-col gap-2.5" aria-label="Grade factors">
        {factors.map((factor) => (
          <li key={factor.factor} className="flex flex-col gap-1">
            <div className="flex flex-wrap items-baseline justify-between gap-x-3 text-sm">
              <span className="font-semibold">
                {GRADE_FACTOR_LABELS[factor.factor] ?? factor.factor}
              </span>
              <span className="text-gray-dark text-xs">
                <span className="font-semibold text-black">{factor.score}</span>{" "}
                · weight {formatWeight(factor.weight)}
                {!factor.hasData && " · no data, neutral score"}
              </span>
            </div>
            <div
              role="meter"
              aria-label={`${GRADE_FACTOR_LABELS[factor.factor] ?? factor.factor} score`}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={factor.score}
              className="bg-gray-light h-2 w-full overflow-hidden rounded-full"
            >
              <div
                className={`h-full rounded-full ${
                  factor.hasData ? "bg-purple" : "bg-purple-soft"
                }`}
                style={{
                  width: `${Math.max(0, Math.min(100, factor.score))}%`,
                }}
              />
            </div>
            <p className="text-gray-dark text-xs break-words">
              {factor.reason}
            </p>
          </li>
        ))}
      </ul>

      {grade.caps.length > 0 && (
        <div className="bg-yellow-tint flex flex-col gap-1 rounded-lg px-3 py-2">
          <h5 className="text-sm font-semibold">
            {ungraded ? "Why it has no grade" : "Held down by"}
          </h5>
          <ul className="text-gray-dark ml-5 list-disc text-sm">
            {grade.caps.map((cap) => (
              <li key={cap} className="break-words">
                {cap}
              </li>
            ))}
          </ul>
        </div>
      )}

      <p className="text-gray-dark text-xs">
        A listing and learner-evidence grade, not a certification of the course
        content. Rubric {grade.rubricVersion}.
      </p>
    </div>
  );
};

export default GradeBreakdown;
