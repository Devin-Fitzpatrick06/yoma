import axios from "axios";
import ApiClient from "~/lib/axiosClient";
import type { PartnerBadge } from "../models/pathway";
import { partnerBadgeIds } from "./partnerBadges.helpers";

// ─────────────────────────────────────────────────────────────────────────────
// Partner badges for a page of course cards (iteration 4, W2): one request for every organisation on
// the page. GET /pathway/badges/organizations?ids=a&ids=b (anonymous). 404 (Pathway:Enabled off)
// resolves to no badges, so cards simply show none.
// ─────────────────────────────────────────────────────────────────────────────

export {
  partnerBadgeIds,
  partnerBadgesQueryKey,
} from "./partnerBadges.helpers";

/** At most this many organisations in one request, so the query string stays short. */
export const PARTNER_BADGES_MAX_IDS = 100;

export const getPartnerBadges = async (
  organizationIds: (string | null | undefined)[],
  signal?: AbortSignal,
): Promise<PartnerBadge[]> => {
  const ids = partnerBadgeIds(organizationIds).slice(0, PARTNER_BADGES_MAX_IDS);
  if (ids.length === 0) return [];

  const query = ids.map((id) => `ids=${encodeURIComponent(id)}`).join("&");
  try {
    const instance = await ApiClient;
    const { data } = await instance.get<PartnerBadge[]>(
      `/pathway/badges/organizations?${query}`,
      { signal },
    );
    return Array.isArray(data) ? data : [];
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) return [];
    throw error;
  }
};
