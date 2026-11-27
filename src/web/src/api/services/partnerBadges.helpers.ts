import type { PartnerBadge, PartnerBadgeLevel } from "../models/pathway";

// Pure parts of the batched partner-badge fetch, unit tested without axios.

/** Unique, non-empty, sorted: the same organisations always make the same request. */
export const partnerBadgeIds = (
  organizationIds: (string | null | undefined)[],
): string[] =>
  [
    ...new Set(
      organizationIds
        .map((id) => id?.trim().toLowerCase())
        .filter((id): id is string => !!id),
    ),
  ].sort();

/** The react-query key: one cache entry per set of organisations, whatever their order. */
export const partnerBadgesQueryKey = (
  organizationIds: (string | null | undefined)[],
) => ["pathway", "partnerBadges", partnerBadgeIds(organizationIds)] as const;

/** Only earned levels show on a card; "None" and "Not enough graded courses" show nothing. */
export const isEarnedPartnerBadge = (
  level: PartnerBadgeLevel | null | undefined,
): level is "Bronze" | "Silver" | "Gold" | "Platinum" =>
  level === "Bronze" ||
  level === "Silver" ||
  level === "Gold" ||
  level === "Platinum";

/** "1 Jan 2027" for the badge's ISO review date; null when it is missing or not a date. */
export const formatReviewDue = (
  iso: string | null | undefined,
): string | null => {
  if (!iso) return null;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return null;
  return new Intl.DateTimeFormat("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  }).format(date);
};

export const reviewDueLine = (iso: string | null | undefined): string => {
  const due = formatReviewDue(iso);
  return due
    ? `Reviewed each quarter · next review ${due}`
    : "Reviewed each quarter";
};

export const partnerBadgeMap = (
  badges: PartnerBadge[] | null | undefined,
): Map<string, PartnerBadge> =>
  new Map(
    (badges ?? []).map((badge) => [
      badge.organizationId.trim().toLowerCase(),
      badge,
    ]),
  );
