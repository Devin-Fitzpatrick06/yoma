import Link from "next/link";
import { FaCheck } from "react-icons/fa";
import type { PathwayCourse } from "~/api/models/pathway";
import GradeChip from "~/components/CourseQuality/GradeChip";
import { COURSE_QUALITY_DEMO } from "~/components/CourseQuality/DemoViewSwitcher";
import { asCourseGrade, outcomeLines } from "~/lib/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// One course on the path: its number, what it adds, why it sits here, Zeki's grade, the youth's
// status and, when the API has enough learners to say, what happened to those who completed it.
// ─────────────────────────────────────────────────────────────────────────────

const STATUS = {
  Completed: { label: "Completed", className: "bg-green-light text-green" },
  InProgress: { label: "In progress", className: "bg-yellow-tint text-black" },
} as const;

const PathwayCourseRow: React.FC<{ course: PathwayCourse; number: number }> = ({
  course,
  number,
}) => {
  const grade = asCourseGrade(course.grade);
  const outcomes = outcomeLines(course.outcome);
  const done = course.status === "Completed";
  const status = course.status === "NotStarted" ? null : STATUS[course.status];
  const titleId = `pathway-course-${course.opportunityId}`;
  const meta = [
    course.organizationName,
    course.difficulty,
    course.hours !== null ? `${course.hours} h` : "Length not listed",
    course.completedCount > 0
      ? `${course.completedCount.toLocaleString("en")} completed on Yoma`
      : null,
  ].filter(Boolean);

  return (
    <article
      aria-labelledby={titleId}
      className={`grid grid-cols-[auto_1fr] gap-x-3 gap-y-2 rounded-xl border bg-white p-4 sm:grid-cols-[auto_1fr_auto] ${
        done ? "border-green-light" : "border-gray"
      }`}
    >
      <span
        aria-hidden="true"
        className={`font-nunito flex h-7 w-7 items-center justify-center rounded-full text-sm font-black tabular-nums ${
          done ? "bg-green text-white" : "bg-purple-tint text-purple"
        }`}
      >
        {done ? <FaCheck className="h-3 w-3" /> : number}
      </span>

      <div className="flex min-w-0 flex-col gap-1.5">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <h4
            id={titleId}
            className="text-base font-semibold tracking-normal text-black"
          >
            <span className="sr-only">Course {number}: </span>
            <Link
              href={`/opportunities/${encodeURIComponent(course.opportunityId)}`}
              className="focus-visible:outline-purple rounded hover:underline focus-visible:outline-2 focus-visible:outline-offset-2"
            >
              {course.title}
            </Link>
          </h4>
          <GradeChip grade={grade} label={course.gradeLabel} />
          {status && (
            <span
              className={`rounded-full px-2 py-0.5 text-xs font-semibold whitespace-nowrap ${status.className}`}
            >
              {status.label}
            </span>
          )}
        </div>

        <p className="text-gray-dark flex flex-wrap gap-x-3 gap-y-0.5 text-xs">
          {meta.map((item) => (
            <span key={item}>{item}</span>
          ))}
        </p>

        {course.skillsAdded.length > 0 && (
          <p className="text-sm">
            Adds <b className="text-purple">{course.skillsAdded.join(", ")}</b>
          </p>
        )}
        {course.reason && (
          <p className="text-gray-dark text-sm">{course.reason}</p>
        )}

        {(outcomes.length > 0 || course.dropOffFlag) && (
          <p className="flex flex-wrap gap-1.5 text-xs">
            {outcomes.map((line) => (
              <span
                key={line}
                title={course.outcome?.note}
                className="bg-green-light text-green rounded-md px-2 py-0.5 font-semibold"
              >
                {line}
              </span>
            ))}
            {COURSE_QUALITY_DEMO && outcomes.length > 0 && (
              <span className="text-gray-dark italic">
                Demo data: synthetic learners
              </span>
            )}
            {course.dropOffFlag && (
              <span
                className="bg-orange-light text-orange rounded-md px-2 py-0.5 font-semibold"
                title="Fewer learners continue into this course than into the others on the path"
              >
                Learners often stop here
              </span>
            )}
          </p>
        )}
      </div>

      <Link
        href={`/opportunities/${encodeURIComponent(course.opportunityId)}`}
        aria-describedby={titleId}
        className="text-purple focus-visible:outline-purple col-start-2 self-start rounded text-sm font-semibold whitespace-nowrap hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 sm:col-start-3"
      >
        {done ? "View again" : "View course"} →
      </Link>
    </article>
  );
};

export default PathwayCourseRow;
