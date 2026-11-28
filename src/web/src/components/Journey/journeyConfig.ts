import { env } from "~/env.mjs";
import { toJourneyConfig } from "~/lib/journey";

// The journey's build-time settings (NEXT_PUBLIC_JOURNEY_ENABLED, NEXT_PUBLIC_JOURNEY_LISTING_ID,
// NEXT_PUBLIC_JOURNEY_YOID, NEXT_PUBLIC_PATHFINDER_URL), with the contract's defaults.
export const JOURNEY_CONFIG = toJourneyConfig({
  enabled: env.NEXT_PUBLIC_JOURNEY_ENABLED === true,
  listingId: env.NEXT_PUBLIC_JOURNEY_LISTING_ID,
  yoid: env.NEXT_PUBLIC_JOURNEY_YOID,
  pathfinderUrl: env.NEXT_PUBLIC_PATHFINDER_URL,
});

/** The journey listing's page on this site (relative), or null when no listing is configured. */
export const JOURNEY_LISTING_HREF = JOURNEY_CONFIG.listingId
  ? `/opportunities/${encodeURIComponent(JOURNEY_CONFIG.listingId)}`
  : null;

/** The journey UI shows only with the switch on and a listing configured. */
export const JOURNEY_ACTIVE = JOURNEY_CONFIG.enabled && !!JOURNEY_LISTING_HREF;
