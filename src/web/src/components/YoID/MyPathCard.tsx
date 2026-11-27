import Link from "next/link";
import { IoArrowForward } from "react-icons/io5";
import type { MyPathSummary } from "~/api/models/pathway";
import {
  MY_PATH_HREF,
  opportunityHref,
  ringStroke,
  toPathProgress,
  toStageProgress,
} from "./MyPathCard.model";

// ─────────────────────────────────────────────────────────────────────────────
// YoID "My path" card (W3 write-back, NEXT_PUBLIC_PATHWAY_ENABLED only): the goal, a progress
// ring, a bar per stage, the next step and a link to the journey on My Yoma. Without a path
// (`summary` null) it shows a small "Start your path" call to action instead.
// ─────────────────────────────────────────────────────────────────────────────

const RING_RADIUS = 30;
const RING_STROKE = 8;
const RING_SIZE = (RING_RADIUS + RING_STROKE) * 2;

const ProgressRing: React.FC<{ percent: number; label: string }> = ({
  percent,
  label,
}) => {
  const { circumference, offset } = ringStroke(percent, RING_RADIUS);
  const centre = RING_SIZE / 2;
  return (
    <div
      role="progressbar"
      aria-label="Path progress"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={percent}
      aria-valuetext={label}
      className="relative shrink-0"
      style={{ width: RING_SIZE, height: RING_SIZE }}
    >
      <svg
        aria-hidden="true"
        width={RING_SIZE}
        height={RING_SIZE}
        viewBox={`0 0 ${RING_SIZE} ${RING_SIZE}`}
        className="-rotate-90"
      >
        <circle
          cx={centre}
          cy={centre}
          r={RING_RADIUS}
          fill="none"
          strokeWidth={RING_STROKE}
          className="stroke-gray-light"
        />
        <circle
          cx={centre}
          cy={centre}
          r={RING_RADIUS}
          fill="none"
          strokeWidth={RING_STROKE}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          className="stroke-green motion-safe:transition-[stroke-dashoffset] motion-safe:duration-700"
        />
      </svg>
      <span
        aria-hidden="true"
        className="font-nunito absolute inset-0 flex items-center justify-center text-base font-black text-black"
      >
        {percent}%
      </span>
    </div>
  );
};

const StartPathCta: React.FC = () => (
  <div className="flex grow flex-col items-center justify-center gap-2 text-center">
    <span
      aria-hidden="true"
      className="flex h-10 w-10 items-center justify-center rounded-full bg-white text-xl shadow-lg"
    >
      🧭
    </span>
    <p className="text-gray-dark text-xs md:text-sm">
      Answer a couple of questions and get a path from where you are to a first
      job.
    </p>
    <Link
      href={MY_PATH_HREF}
      className="btn btn-sm bg-green hover:bg-green-dark rounded-full border-none px-4 text-white normal-case"
    >
      Start your path
    </Link>
  </div>
);

export const MyPathCard: React.FC<{ summary: MyPathSummary | null }> = ({
  summary,
}) => {
  if (!summary) return <StartPathCta />;

  const progress = toPathProgress(summary);
  const stages = toStageProgress(summary.stages);
  const finished = progress.total > 0 && progress.completed >= progress.total;

  return (
    <div className="flex grow flex-col gap-3 text-xs md:text-sm">
      {/* GOAL + RING */}
      <div className="flex items-center gap-3">
        <ProgressRing percent={progress.percent} label={progress.label} />
        <div className="flex min-w-0 flex-col gap-0.5">
          <span className="text-gray-dark text-xs">Your goal</span>
          <span className="font-nunito line-clamp-2 text-sm font-black break-words text-black md:text-base">
            {summary.goalLabel}
          </span>
          <span className="text-gray-dark text-xs">{progress.label}</span>
        </div>
      </div>

      {/* STAGES */}
      {stages.length > 0 && (
        <ol aria-label="Stages" className="flex gap-1.5">
          {stages.map((stage, index) => (
            <li
              key={`${index}-${stage.name}`}
              className="flex min-w-0 flex-1 flex-col gap-1"
              title={stage.label}
            >
              <span className="sr-only">{stage.label}</span>
              <span
                aria-hidden="true"
                className="bg-gray-light block h-1.5 w-full overflow-hidden rounded-full"
              >
                <span
                  className={`block h-full rounded-full ${stage.done ? "bg-green" : "bg-green-dark"}`}
                  style={{ width: `${stage.percent}%` }}
                />
              </span>
              <span
                aria-hidden="true"
                className="text-gray-dark truncate text-[10px] leading-tight md:text-xs"
              >
                {stage.name}
              </span>
            </li>
          ))}
        </ol>
      )}

      {/* NEXT STEP + JOURNEY */}
      <div className="mt-auto flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        {summary.nextStep ? (
          <Link
            href={opportunityHref(summary.nextStep.opportunityId)}
            className="flex min-w-0 items-baseline gap-1 hover:underline"
          >
            <span className="text-gray-dark shrink-0">Next:</span>
            <span className="truncate font-semibold text-black">
              {summary.nextStep.title}
            </span>
          </Link>
        ) : (
          <span className="font-semibold text-black">
            {finished ? "Path complete 🎉" : "No next step yet"}
          </span>
        )}
        <Link
          href={MY_PATH_HREF}
          className="text-green inline-flex shrink-0 items-center gap-1 font-semibold hover:underline"
        >
          Open my journey
          <IoArrowForward aria-hidden="true" className="h-4 w-4" />
        </Link>
      </div>
    </div>
  );
};
