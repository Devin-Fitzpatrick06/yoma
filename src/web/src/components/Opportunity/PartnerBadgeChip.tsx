import { useQuery } from "@tanstack/react-query";
import { createContext, useContext, useMemo, type ReactNode } from "react";
import type { PartnerBadge, PartnerBadgeLevel } from "~/api/models/pathway";
import {
  getPartnerBadges,
  partnerBadgeIds,
  partnerBadgesQueryKey,
} from "~/api/services/partnerBadges";
import {
  isEarnedPartnerBadge,
  partnerBadgeMap,
} from "~/api/services/partnerBadges.helpers";
import { env } from "~/env.mjs";
import { PARTNER_BADGE_LABELS } from "~/lib/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// A partner's badge on course cards and the course page header (iteration 4, W2): "Gold partner".
// A list fetches every organisation's badge in ONE request (`PartnerBadgesProvider`); a chip reads
// its organisation from the nearest provider and renders nothing while loading, without a provider,
// or for "None" and "Not enough graded courses". Off unless NEXT_PUBLIC_PATHWAY_ENABLED.
// ─────────────────────────────────────────────────────────────────────────────

const PartnerBadgesContext = createContext<Map<string, PartnerBadge> | null>(
  null,
);

/** Whether a provider above already serves the badges (so a nested list does not fetch again). */
export const useHasPartnerBadgesProvider = () =>
  useContext(PartnerBadgesContext) !== null;

export const PartnerBadgesProvider: React.FC<{
  organizationIds: (string | null | undefined)[];
  children: ReactNode;
}> = ({ organizationIds, children }) => {
  const ids = partnerBadgeIds(organizationIds);
  const enabled = env.NEXT_PUBLIC_PATHWAY_ENABLED && ids.length > 0;
  const { data } = useQuery({
    queryKey: partnerBadgesQueryKey(ids),
    queryFn: ({ signal }) => getPartnerBadges(ids, signal),
    enabled,
    staleTime: 15 * 60 * 1000, // badges change once a quarter
    retry: false,
  });
  const badges = useMemo(() => partnerBadgeMap(data), [data]);
  return (
    <PartnerBadgesContext.Provider value={badges}>
      {children}
    </PartnerBadgesContext.Provider>
  );
};

/** Wraps a list in a provider unless one above already covers the page. */
export const PartnerBadgesScope: React.FC<{
  organizationIds: (string | null | undefined)[];
  children: ReactNode;
}> = ({ organizationIds, children }) =>
  useHasPartnerBadgesProvider() ? (
    <>{children}</>
  ) : (
    <PartnerBadgesProvider organizationIds={organizationIds}>
      {children}
    </PartnerBadgesProvider>
  );

export const usePartnerBadge = (
  organizationId: string | null | undefined,
): PartnerBadge | null => {
  const badges = useContext(PartnerBadgesContext);
  if (!badges || !organizationId) return null;
  return badges.get(organizationId.trim().toLowerCase()) ?? null;
};

// Black or white text on every level, for contrast at this size; the dot carries the metal.
const CHIP_STYLE: Record<
  "Bronze" | "Silver" | "Gold" | "Platinum",
  { chip: string; dot: string }
> = {
  Platinum: { chip: "bg-purple text-white", dot: "bg-purple-tint" },
  Gold: { chip: "bg-yellow-light text-black", dot: "bg-yellow" },
  Silver: { chip: "bg-gray text-black", dot: "bg-gray-dark" },
  Bronze: { chip: "bg-orange-light text-black", dot: "bg-orange" },
};

/** The chip itself, for a known level (also used by the preview). */
export const PartnerBadgeLevelChip: React.FC<{
  level: PartnerBadgeLevel | null | undefined;
  className?: string;
}> = ({ level, className = "" }) => {
  if (!isEarnedPartnerBadge(level)) return null;
  return (
    <span
      className={`inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 align-middle text-[11px] leading-4 font-bold whitespace-nowrap ${CHIP_STYLE[level].chip} ${className}`}
      title={`${PARTNER_BADGE_LABELS[level]}: rated by Ziqi on its graded courses`}
    >
      <span
        aria-hidden="true"
        className={`h-1.5 w-1.5 rounded-full ${CHIP_STYLE[level].dot}`}
      />
      {PARTNER_BADGE_LABELS[level]}
    </span>
  );
};

/** The organisation's chip from the nearest provider; nothing when it has no earned badge. */
const PartnerBadgeChip: React.FC<{
  organizationId: string | null | undefined;
  className?: string;
}> = ({ organizationId, className }) => {
  const badge = usePartnerBadge(organizationId);
  return <PartnerBadgeLevelChip level={badge?.level} className={className} />;
};

export default PartnerBadgeChip;
