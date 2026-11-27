import Link from "next/link";
import {
  FaCheck,
  FaExchangeAlt,
  FaGraduationCap,
  FaHandsHelping,
  FaLock,
  FaLockOpen,
  FaRegClock,
  FaTasks,
} from "react-icons/fa";
import type { PathwayCourse, PathwayStepType } from "~/api/models/pathway";
import GradeChip from "~/components/CourseQuality/GradeChip";
import { asCourseGrade, percent } from "~/lib/pathway";
import styles from "./JourneyMap.module.css";
import {
  dropOffLine,
  hoursLabel,
  STEP_TYPE_LABELS,
  topNextSteps,
  visiblePartnerBadge,
} from "./journeyMap";

// ─────────────────────────────────────────────────────────────────────────────
// One step on the journey map: what it is, who runs it, Ziqi's grade, what it adds and why it is
// here, where completers usually go next, the reserved drop-off slot, and Lock / Swap. The title
// link covers the card, so a click anywhere opens the opportunity; the buttons sit above it.
// ─────────────────────────────────────────────────────────────────────────────

export const STEP_TYPE_ICONS: Record<PathwayStepType, typeof FaGraduationCap> =
  {
    Learning: FaGraduationCap,
    ImpactAction: FaHandsHelping,
    Task: FaTasks,
  };

const BADGE_STYLE = {
  Platinum: { icon: "💎", className: `${styles.platinum} text-purple` },
  Gold: { icon: "🥇", className: "bg-yellow-tint text-black" },
  Silver: { icon: "🥈", className: "bg-gray-light text-black" },
  Bronze: { icon: "🥉", className: "bg-orange-light text-black" },
} as const;

export const PartnerBadgeChip: React.FC<{
  level: PathwayCourse["partnerBadge"];
}> = ({ level }) => {
  const visible = visiblePartnerBadge(level);
  if (!visible) return null;
  const style = BADGE_STYLE[visible];
  return (
    <span
      className={`inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold whitespace-nowrap ${style.className}`}
    >
      <span aria-hidden="true">{style.icon}</span>
      {visible} partner
    </span>
  );
};

const StepCard: React.FC<{
  course: PathwayCourse;
  number: number;
  total: number;
  isHere: boolean;
  locked: boolean;
  lockPending: boolean;
  lockError: string | null;
  lockAnimKey: number;
  showDemoLabel: boolean;
  className?: string;
  style?: React.CSSProperties;
  onOpen: () => void;
  onToggleLock: () => void;
  onSwap: () => void;
}> = ({
  course,
  number,
  total,
  isHere,
  locked,
  lockPending,
  lockError,
  lockAnimKey,
  showDemoLabel,
  className = "",
  style,
  onOpen,
  onToggleLock,
  onSwap,
}) => {
  const done = course.status === "Completed";
  const Icon = STEP_TYPE_ICONS[course.type] ?? FaGraduationCap;
  const typeLabel = STEP_TYPE_LABELS[course.type] ?? "Course";
  const titleId = `journey-step-${course.opportunityId}`;
  const next = topNextSteps(course.usuallyNext);

  return (
    <article
      data-step-id={course.opportunityId}
      aria-labelledby={titleId}
      style={style}
      className={`group has-[a:focus-visible]:ring-purple relative flex min-w-0 flex-1 flex-col gap-2 rounded-2xl border bg-white p-3 shadow-sm transition-shadow duration-200 hover:shadow-md has-[a:focus-visible]:ring-2 has-[a:focus-visible]:ring-offset-2 sm:p-4 ${
        done
          ? "border-green-light"
          : isHere
            ? "border-purple-soft"
            : "border-gray"
      } ${className}`}
    >
      <div className="flex flex-wrap items-center gap-1.5">
        <span
          className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold ${
            course.type === "Learning"
              ? "bg-blue-light text-blue-dark"
              : "bg-green-light text-green"
          }`}
        >
          <Icon aria-hidden="true" className="h-3 w-3" />
          {typeLabel}
        </span>
        {done ? (
          <span className="bg-green inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold text-white">
            <FaCheck aria-hidden="true" className="h-2.5 w-2.5" />
            Done
          </span>
        ) : isHere ? (
          <span className="bg-purple inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold text-white">
            You are here
          </span>
        ) : null}
        {course.status === "InProgress" && (
          <span className="bg-yellow-tint rounded-full px-2 py-0.5 text-xs font-semibold text-black">
            In progress
          </span>
        )}
        {locked && (
          <span className="text-purple bg-purple-tint inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold">
            <FaLock aria-hidden="true" className="h-2.5 w-2.5" />
            Locked
          </span>
        )}
      </div>

      <h4
        id={titleId}
        className="font-nunito text-base leading-snug font-black tracking-normal text-black"
      >
        <span className="sr-only">
          Step {number} of {total}, {typeLabel.toLowerCase()}:{" "}
        </span>
        <Link
          href={`/opportunities/${encodeURIComponent(course.opportunityId)}`}
          onClick={onOpen}
          className="group-hover:underline after:absolute after:inset-0 after:rounded-2xl after:content-[''] focus-visible:outline-none"
        >
          {course.title}
        </Link>
      </h4>

      <p className="text-gray-dark flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
        <span className="min-w-0 truncate font-semibold">
          {course.organizationName}
        </span>
        <PartnerBadgeChip level={course.partnerBadge} />
      </p>

      <p className="flex flex-wrap items-center gap-1.5 text-xs">
        <GradeChip
          grade={asCourseGrade(course.grade)}
          label={course.gradeLabel}
        />
        <span className="text-gray-dark inline-flex items-center gap-1">
          <FaRegClock aria-hidden="true" className="h-3 w-3" />
          {hoursLabel(course.hours)}
        </span>
      </p>

      {course.skillsAdded.length > 0 && (
        <p className="text-sm">
          Adds <b className="text-purple">{course.skillsAdded.join(", ")}</b>
        </p>
      )}
      {course.reason && (
        <p className="text-gray-dark text-sm leading-snug">{course.reason}</p>
      )}

      {next.length > 0 && (
        <div className="bg-blue-light rounded-lg px-2.5 py-2 text-xs leading-snug">
          <p className="font-semibold text-black">
            Young people who completed this usually go on to
          </p>
          <ul className="mt-0.5 flex flex-col gap-0.5">
            {next.map((n) => (
              <li key={n.opportunityId} className="text-gray-dark">
                <b className="text-black">{n.title}</b>{" "}
                <span className="whitespace-nowrap">
                  ({percent(n.share)}% of {n.count.toLocaleString("en")})
                </span>
              </li>
            ))}
          </ul>
          {showDemoLabel && (
            <p className="text-gray-dark mt-0.5 italic">
              Demo data: synthetic learners
            </p>
          )}
        </div>
      )}

      {/* Reserved for drop-off (W5): a note while collecting, never a number. */}
      <p className="text-gray-dark flex min-h-4 items-center gap-1.5 text-[11px] leading-4">
        <span
          aria-hidden="true"
          className="bg-gray h-1.5 w-1.5 shrink-0 rounded-full"
        />
        <span>{dropOffLine(course.dropOff?.note)}</span>
      </p>

      <div className="relative z-10 mt-auto flex flex-wrap items-center gap-2 pt-1">
        <button
          type="button"
          onClick={onToggleLock}
          disabled={lockPending}
          aria-pressed={locked}
          aria-label={
            locked
              ? `Unlock ${course.title}`
              : `Lock ${course.title} so it stays when your path is rebuilt`
          }
          className={`focus-visible:outline-purple inline-flex min-h-9 items-center gap-1.5 rounded-full border px-3 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:opacity-60 ${
            locked
              ? "border-purple bg-purple text-white"
              : "border-gray text-purple hover:border-purple-soft bg-white"
          }`}
        >
          <span
            key={lockAnimKey}
            className={lockAnimKey ? styles.lockToggle : ""}
          >
            {locked ? (
              <FaLock aria-hidden="true" className="h-3 w-3" />
            ) : (
              <FaLockOpen aria-hidden="true" className="h-3 w-3" />
            )}
          </span>
          {locked ? "Locked" : "Lock"}
        </button>
        {!done && (
          <button
            type="button"
            data-swap=""
            onClick={onSwap}
            disabled={locked}
            title={locked ? "Unlock this step to swap it" : undefined}
            aria-label={`Swap ${course.title} for another option`}
            aria-haspopup="dialog"
            className="focus-visible:outline-purple border-gray text-purple hover:border-purple-soft inline-flex min-h-9 items-center gap-1.5 rounded-full border bg-white px-3 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <FaExchangeAlt aria-hidden="true" className="h-3 w-3" />
            Swap
          </button>
        )}
        {lockError && (
          <span role="alert" className="text-pink text-xs">
            {lockError}
          </span>
        )}
      </div>
    </article>
  );
};

export default StepCard;
