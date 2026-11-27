import { useId } from "react";
import type { PartnerBadge, PartnerBadgeLevel } from "~/api/models/pathway";
import { reviewDueLine } from "~/api/services/partnerBadges.helpers";
import { PARTNER_BADGE_LABELS, percent } from "~/lib/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// A partner's badge on its Ziqi page and the organisation page: the level, the figures behind it,
// the hint for reaching the next level and when it is next reviewed. Small partners see why they
// have no badge yet rather than a low one. Platinum, the top level, has its own purple card.
// ─────────────────────────────────────────────────────────────────────────────

const LEVEL_STYLE: Record<
  PartnerBadgeLevel,
  { icon: string; ring: string; card: string }
> = {
  Platinum: {
    icon: "💎",
    ring: "bg-white ring-2 ring-purple-soft",
    card: "bg-purple-tint ring-1 ring-purple-soft",
  },
  Gold: { icon: "🥇", ring: "bg-yellow-tint", card: "bg-white" },
  Silver: { icon: "🥈", ring: "bg-gray-light", card: "bg-white" },
  Bronze: { icon: "🥉", ring: "bg-orange-light", card: "bg-white" },
  None: { icon: "🏅", ring: "bg-gray-light", card: "bg-white" },
  NotEnoughGraded: { icon: "⏳", ring: "bg-gray-light", card: "bg-white" },
};

const PartnerBadgeCard: React.FC<{ badge: PartnerBadge }> = ({ badge }) => {
  const headingId = useId();
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
      aria-labelledby={headingId}
      className={`flex flex-col gap-3 rounded-lg p-4 shadow-sm sm:flex-row sm:items-center md:p-5 ${style.card}`}
    >
      <span
        aria-hidden="true"
        className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-full text-2xl ${style.ring}`}
      >
        {style.icon}
      </span>
      <div className="flex min-w-0 flex-col gap-1">
        <p className="text-gray-dark text-xs">Ziqi partner badge</p>
        <h2
          id={headingId}
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
        <p className="text-gray-dark text-xs">
          {reviewDueLine(badge.reviewDue)}
        </p>
      </div>
    </section>
  );
};

export default PartnerBadgeCard;
