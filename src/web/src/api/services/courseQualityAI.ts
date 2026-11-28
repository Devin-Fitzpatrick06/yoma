import ApiClient from "~/lib/axiosClient";
import type {
  CourseQualityAIUsageSummary,
  CourseQualityAssessment,
} from "../models/courseQuality";

// Ziqi iteration 5: the live AI check behind an assessment and what AI runs have used.

/** Under ["courseQuality"], so a decision or a new AI run refreshes it too. */
export const AI_USAGE_QUERY_KEY = ["courseQuality", "ai", "usage"];

/**
 * A local model on a small machine can take minutes to answer, so the AI check waits much
 * longer than the client's usual 45 s "slow network" abort.
 */
export const AI_CHECK_TIMEOUT_MS = 600_000; // 10 minutes

/** GET /courseQuality/ai/usage (Admin): the configured model and what its runs have used. */
export const getCourseQualityAIUsage =
  async (): Promise<CourseQualityAIUsageSummary> => {
    const instance = await ApiClient;
    const { data } = await instance.get<CourseQualityAIUsageSummary>(
      "/courseQuality/ai/usage",
    );
    return data;
  };

/**
 * POST /courseQuality/opportunity/{id}/assess (Admin): assess the opportunity again on the
 * configured model. The returned assessment carries `aiUsage` when an AI ran.
 */
export const runCourseQualityAICheck = async (
  opportunityId: string,
): Promise<CourseQualityAssessment> => {
  const instance = await ApiClient;
  const { data } = await instance.post<CourseQualityAssessment>(
    `/courseQuality/opportunity/${encodeURIComponent(opportunityId)}/assess`,
    undefined,
    { timeout: AI_CHECK_TIMEOUT_MS },
  );
  return data;
};
