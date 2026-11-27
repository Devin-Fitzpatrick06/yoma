import ApiClient from "~/lib/axiosClient";
import type { DraftCheckResult } from "../models/courseQuality";
import type { OpportunityRequestBase } from "../models/opportunity";

// ─────────────────────────────────────────────────────────────────────────────
// Ziqi pre-publish check (iteration 4, W1): the partner form sends the listing as it stands and gets
// Ziqi's predicted verdict and fixes. Nothing is stored. 404 unless CourseQuality:PrePublish:Enabled.
// ─────────────────────────────────────────────────────────────────────────────

export const checkDraftListing = async (
  organizationId: string,
  draft: Partial<OpportunityRequestBase>,
  signal?: AbortSignal,
): Promise<DraftCheckResult> => {
  const instance = await ApiClient;
  const { data } = await instance.post<DraftCheckResult>(
    `/courseQuality/organization/${organizationId}/draft/check`,
    draft,
    { signal },
  );
  return data;
};
