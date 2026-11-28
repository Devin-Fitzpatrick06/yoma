import { useQuery } from "@tanstack/react-query";
import { isOpportunitySaved } from "~/api/services/myOpportunities";
import { JOURNEY_ACTIVE, JOURNEY_CONFIG } from "./journeyConfig";

/**
 * Whether the signed-in youth has saved the journey listing (Yoma's own
 * `GET /myopportunity/action/{id}/saved`), for the YoID and My Yoma journey cards. Off (no
 * request, `false`) unless the journey is on and `enabled`. Never stale: the save happens on the
 * listing page, which does not share this cache.
 */
export const useJourneyListingSaved = (enabled: boolean) => {
  const listingId = JOURNEY_CONFIG.listingId;
  const query = useQuery({
    queryKey: ["Journey", "ListingSaved", listingId],
    queryFn: async () => !!(await isOpportunitySaved(listingId!)),
    enabled: JOURNEY_ACTIVE && enabled && !!listingId,
    staleTime: 0,
    retry: false,
  });
  return { saved: query.data === true, isLoading: query.isLoading };
};
