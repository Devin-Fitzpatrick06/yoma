import type { LearnerBadge, LearnerBadgeKey } from "~/api/models/pathway";
import { badgeProgress, sortBadges } from "~/lib/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// The learner badges strip: earned badges in colour, the rest greyed with progress towards them.
// Scrolls sideways on phones rather than wrapping into a tall block.
// ─────────────────────────────────────────────────────────────────────────────

const ICONS: Record<LearnerBadgeKey, string> = {
  Pathfinder: "🧭",
  FirstStep: "👣",
  StageComplete: "🏁",
  HalfwayThere: "⛰️",
  JobReady: "💼",
  VerifiedSkill: "✅",
  TopPickLearner: "🌟",
};

const LearnerBadges: React.FC<{ badges: LearnerBadge[] }> = ({ badges }) => {
  if (badges.length === 0) return null;
  const earned = badges.filter((b) => b.earned).length;

  return (
    <section aria-labelledby="learner-badges" className="flex flex-col gap-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2
          id="learner-badges"
          className="font-nunito text-lg font-black text-black"
        >
          Your badges
        </h2>
        <p className="text-gray-dark text-xs">
          {earned} of {badges.length} earned
        </p>
      </div>
      {/* focusable, so the strip can be scrolled with the keyboard */}
      <ul
        tabIndex={0}
        aria-labelledby="learner-badges"
        className="focus-visible:outline-purple -mx-2 flex snap-x gap-2.5 overflow-x-auto rounded-lg px-2 pb-2 focus-visible:outline-2"
      >
        {sortBadges(badges).map((badge) => {
          const progress = badgeProgress(badge);
          return (
            <li
              key={badge.key}
              className={`flex w-36 shrink-0 snap-start flex-col gap-1 rounded-xl border p-3 ${
                badge.earned
                  ? "border-purple-light bg-white"
                  : "border-gray bg-gray-light"
              }`}
            >
              <span
                aria-hidden="true"
                className={`text-2xl ${badge.earned ? "" : "opacity-40 grayscale"}`}
              >
                {ICONS[badge.key] ?? "🏅"}
              </span>
              <span className="text-sm font-semibold text-black">
                {badge.label}
              </span>
              <span className="text-gray-dark text-xs leading-snug">
                {badge.description}
              </span>
              <span className="mt-auto pt-1 text-xs font-semibold">
                {badge.earned ? (
                  <span className="text-purple">Earned</span>
                ) : progress !== null ? (
                  <>
                    <progress
                      className="progress progress-primary bg-gray h-1 w-full"
                      value={progress}
                      max={100}
                      aria-label={`${badge.label}: ${progress}% of the way`}
                    />
                    <span className="text-gray-dark font-normal">
                      {progress}% of the way
                    </span>
                  </>
                ) : (
                  <span className="text-gray-dark font-normal">
                    Not earned yet
                  </span>
                )}
              </span>
            </li>
          );
        })}
      </ul>
    </section>
  );
};

export default LearnerBadges;
