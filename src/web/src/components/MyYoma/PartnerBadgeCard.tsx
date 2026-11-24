import type { PartnerBadge, PartnerBadgeLevel } from "~/api/models/pathway";
import { PARTNER_BADGE_LABELS, percent } from "~/lib/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// A partner's badge on its Course Quality page: the level, the figures behind it and the hint for
// reaching the next level. Small partners see why they have no badge yet rather than a low one.
// ─────────────────────────────────────────────────────────────────────────────

const LEVEL_STYLE: Record<PartnerBadgeLevel, { icon: string; ring: string }> = {
  Gold: { icon: "🥇", ring: "bg-yellow-tint" },
  Silver: { icon: "🥈", ring: "bg-gray-light" },
  Bronze: { icon: "🥉", ring: "bg-orange-light" },
  None: { icon: "🏅", ring: "bg-gray-light" },
  NotEnoughGraded: { icon: "⏳", ring: "bg-gray-light" },
};

const PartnerBadgeCard: React.FC<{ badge: PartnerBadge }> = ({ badge }) => {
  const style = LEVEL_STYLE[badge.level] ?? LEVEL_STYLE.None;
  const figures = [
    `${badge.gradedCourses.toLocaleString("en")} graded ${
      badge.gradedCourses === 1 ? "course" : "courses"
    }`,
    badge.shareTopGrades !== null
      ? `${percent(badge.shareTopGrades)}% at A or B`
      : null,
    badge.shareHeldBack !== null
      ? `${percent(badge.shareHeldBack)}% not verified`
      : null,
    badge.waitingForReview > 0
      ? `${badge.waitingForReview.toLocaleString("en")} waiting for review`
      : null,
  ].filter(Boolean);

  return (
    <section
      aria-labelledby="partner-badge-title"
      className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm sm:flex-row sm:items-center md:p-5"
    >
      <span
        aria-hidden="true"
        className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-full text-2xl ${style.ring}`}
      >
        {style.icon}
      </span>
      <div className="flex min-w-0 flex-col gap-1">
        <p className="text-gray-dark text-xs">Partner badge</p>
        <h2
          id="partner-badge-title"
          className="font-nunito text-lg font-black text-black"
        >
          {PARTNER_BADGE_LABELS[badge.level] ?? badge.level}
        </h2>
        <p className="text-gray-dark flex flex-wrap gap-x-3 text-xs">
          {figures.map((figure) => (
            <span key={figure}>{figure}</span>
          ))}
        </p>
        {badge.nextLevelHint && (
          <p className="text-sm text-black">{badge.nextLevelHint}</p>
        )}
        {badge.level === "NotEnoughGraded" && !badge.nextLevelHint && (
          <p className="text-sm text-black">
            A badge is given once {badge.minimumGradedCourses} of your courses
            are graded.
          </p>
        )}
      </div>
    </section>
  );
};

export default PartnerBadgeCard;
