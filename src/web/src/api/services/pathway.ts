import axios from "axios";
import ApiClient from "~/lib/axiosClient";
import type {
  CourseOutcome,
  LearnerBadge,
  MyPathSummary,
  MyYoma,
  PartnerBadge,
  PathwayCourse,
  PathwayHealth,
  PathwayProfile,
  PathwayProfileRequest,
  PathwayQuestions,
  PathwayStepEventRequest,
} from "../models/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// "My Yoma" pathways (iteration 3). One function per endpoint in `models/pathway.ts`. A 404 that
// means "nothing here yet" (not onboarded, no outcome) resolves to `null`; every other failure
// throws, so a page can tell "start onboarding" from "could not load".
// ─────────────────────────────────────────────────────────────────────────────

const isNotFound = (error: unknown) =>
  axios.isAxiosError(error) && error.response?.status === 404;

/** The onboarding questions and their options (anonymous). */
export const getPathwayQuestions = async (): Promise<PathwayQuestions> => {
  const instance = await ApiClient;
  const { data } = await instance.get<PathwayQuestions>("/pathway/questions");
  return {
    questions: data?.questions ?? [],
    answeredFromPreferences: data?.answeredFromPreferences ?? [],
  };
};

/** The signed-in youth's answers. `null` when they have not onboarded yet (404). */
export const getPathwayProfile = async (): Promise<PathwayProfile | null> => {
  try {
    const instance = await ApiClient;
    const { data } = await instance.get<PathwayProfile>("/pathway/me/profile");
    return data ?? null;
  } catch (error) {
    if (isNotFound(error)) return null;
    throw error;
  }
};

/** Saves the youth's answers (creates or replaces them) and returns the stored profile. */
export const updatePathwayProfile = async (
  request: PathwayProfileRequest,
): Promise<PathwayProfile> => {
  const instance = await ApiClient;
  const { data } = await instance.put<PathwayProfile>(
    "/pathway/me/profile",
    request,
  );
  return data;
};

/** The youth's path, YoID summary, jobs and badges. `null` when they have not onboarded (404). */
export const getMyYoma = async (): Promise<MyYoma | null> => {
  try {
    const instance = await ApiClient;
    const { data } = await instance.get<MyYoma>("/pathway/me");
    return data ?? null;
  } catch (error) {
    if (isNotFound(error)) return null;
    throw error;
  }
};

/** The signed-in youth's learner badges, earned and in progress. */
export const getMyLearnerBadges = async (): Promise<LearnerBadge[]> => {
  const instance = await ApiClient;
  const { data } = await instance.get<LearnerBadge[]>("/pathway/badges/me");
  return Array.isArray(data) ? data : [];
};

/** A partner's badge (anonymous). `null` when the organisation has none (404). */
export const getPartnerBadge = async (
  organizationId: string,
): Promise<PartnerBadge | null> => {
  try {
    const instance = await ApiClient;
    const { data } = await instance.get<PartnerBadge>(
      `/pathway/badges/organization/${encodeURIComponent(organizationId)}`,
    );
    return data ?? null;
  } catch (error) {
    if (isNotFound(error)) return null;
    throw error;
  }
};

/** What happened to a course's completers (anonymous). `null` when not computed (404). */
export const getCourseOutcome = async (
  opportunityId: string,
): Promise<CourseOutcome | null> => {
  try {
    const instance = await ApiClient;
    const { data } = await instance.get<CourseOutcome>(
      `/pathway/outcomes/opportunity/${encodeURIComponent(opportunityId)}`,
    );
    return data ?? null;
  } catch (error) {
    if (isNotFound(error)) return null;
    throw error;
  }
};

// ── Iteration 4 ──────────────────────────────────────────────────────────────

/** Pins or unpins a step; a pinned step keeps its place when the path is rebuilt. */
export const setPathwayStepLocked = async (
  opportunityId: string,
  locked: boolean,
): Promise<MyYoma> => {
  const instance = await ApiClient;
  const { data } = await instance.put<MyYoma>(
    `/pathway/me/steps/${opportunityId}/lock`,
    { locked },
  );
  return data;
};

/** Up to three alternatives for a step: same stage and step type. */
export const getPathwayStepAlternatives = async (
  opportunityId: string,
): Promise<PathwayCourse[]> => {
  const instance = await ApiClient;
  const { data } = await instance.get<PathwayCourse[]>(
    `/pathway/me/steps/${opportunityId}/alternatives`,
  );
  return data ?? [];
};

/** Replaces a step with one of its alternatives. */
export const swapPathwayStep = async (
  opportunityId: string,
  replacementOpportunityId: string,
): Promise<MyYoma> => {
  const instance = await ApiClient;
  const { data } = await instance.post<MyYoma>(
    `/pathway/me/steps/${opportunityId}/swap`,
    { replacementOpportunityId },
  );
  return data;
};

/** Records a step event for drop-off (fire and forget: a failure never disturbs the page). */
export const recordPathwayStepEvent = async (
  request: PathwayStepEventRequest,
): Promise<void> => {
  try {
    const instance = await ApiClient;
    await instance.post("/pathway/me/events", request);
  } catch {
    // collection only; the page does not depend on it
  }
};

/** Admin: how much step data has been collected, and drop-off once the pilot provides it. */
export const getPathwayHealth = async (): Promise<PathwayHealth> => {
  const instance = await ApiClient;
  const { data } = await instance.get<PathwayHealth>("/pathway/health");
  return data;
};

/** The YoID page's "My path" card. `null` when the youth has no path yet (404). */
export const getMyPathSummary = async (): Promise<MyPathSummary | null> => {
  try {
    const instance = await ApiClient;
    const { data } = await instance.get<MyPathSummary>("/pathway/yoid/me");
    return data;
  } catch (error) {
    if (isNotFound(error)) return null;
    throw error;
  }
};
