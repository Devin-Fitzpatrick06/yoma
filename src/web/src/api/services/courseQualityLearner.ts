import axios from "axios";
import type { GetServerSidePropsContext, GetStaticPropsContext } from "next";
import ApiClient from "~/lib/axiosClient";
import ApiServer from "~/lib/axiosServer";
import type {
  CourseFeedbackOptions,
  CourseFeedbackPrompt,
  CourseFeedbackSubmission,
  CourseFeedbackSummary,
  LearnerCard,
} from "../models/courseQuality";

// The card is an extra on the opportunity page, so it never holds the page up for long.
const LEARNER_CARD_TIMEOUT = 2500;

/**
 * The public learner card of a Learning opportunity. `null` when there is no card (404) or the
 * request fails for any reason: the opportunity page then renders as it does without one.
 */
export const getLearnerCard = async (
  opportunityId: string,
  context?: GetServerSidePropsContext | GetStaticPropsContext,
): Promise<LearnerCard | null> => {
  try {
    const instance = context ? ApiServer(context) : await ApiClient;
    const { data } = await instance.get<LearnerCard>(
      `/courseQuality/opportunity/${encodeURIComponent(opportunityId)}/card`,
      { timeout: LEARNER_CARD_TIMEOUT },
    );
    return data ?? null;
  } catch (error) {
    if (!(axios.isAxiosError(error) && error.response?.status === 404))
      console.error("Error fetching the learner card", error);
    return null;
  }
};

// ─────────────────────────────────────────────────────────────────────────────
// Learner feedback ("Did you end up taking it?"). Every call here runs in the background of
// whatever page the learner is on, so each one stays under the client's slow-network notice
// (10 s) and never surfaces an error to the learner.
// ─────────────────────────────────────────────────────────────────────────────

const FEEDBACK_TIMEOUT = 8000;

const feedbackPath = (opportunityId: string) =>
  `/courseQuality/opportunity/${encodeURIComponent(opportunityId)}/feedback`;

// the tags change only with a deploy, so one fetch per page session is enough
let feedbackOptionsCache: Promise<CourseFeedbackOptions> | null = null;

/** The good and bad tags the survey offers. Cached for the session; a failure is not cached. */
export const getFeedbackOptions = (): Promise<CourseFeedbackOptions> => {
  if (!feedbackOptionsCache) {
    feedbackOptionsCache = (async () => {
      const instance = await ApiClient;
      const { data } = await instance.get<CourseFeedbackOptions>(
        "/courseQuality/feedback/options",
        { timeout: FEEDBACK_TIMEOUT },
      );
      return { good: data?.good ?? [], bad: data?.bad ?? [] };
    })();
    feedbackOptionsCache.catch(() => {
      feedbackOptionsCache = null;
    });
  }
  return feedbackOptionsCache;
};

/**
 * Records that the signed-in learner left Yoma for the course. Fire and forget: it never throws
 * and never delays the navigation, so callers do not await it.
 */
export const recordCourseLeft = (opportunityId: string): void => {
  void (async () => {
    try {
      const instance = await ApiClient;
      await instance.post(`${feedbackPath(opportunityId)}/left`, null, {
        timeout: FEEDBACK_TIMEOUT,
      });
    } catch {
      // a missed "left" only means the learner is not asked later
    }
  })();
};

/** The course the learner is due to be asked about (at most one). Empty on any failure. */
export const getPendingFeedback = async (): Promise<CourseFeedbackPrompt[]> => {
  try {
    const instance = await ApiClient;
    const { data } = await instance.get<CourseFeedbackPrompt[]>(
      "/courseQuality/feedback/pending",
      { timeout: FEEDBACK_TIMEOUT },
    );
    return Array.isArray(data) ? data : [];
  } catch {
    return [];
  }
};

/** Saves the learner's answer. Throws on failure so the caller can decide what to show. */
export const submitCourseFeedback = async (
  opportunityId: string,
  submission: CourseFeedbackSubmission,
): Promise<void> => {
  const instance = await ApiClient;
  await instance.post(feedbackPath(opportunityId), submission, {
    timeout: FEEDBACK_TIMEOUT,
  });
};

/**
 * What learners said about a course, for Yoma reviewers and the course's partner. Comments are
 * empty unless the caller is a Yoma admin. `null` when the course has no summary (404); any other
 * failure throws, so a caller can tell "no answers yet" from "could not load".
 */
export const getFeedbackSummary = async (
  opportunityId: string,
): Promise<CourseFeedbackSummary | null> => {
  try {
    const instance = await ApiClient;
    const { data } = await instance.get<CourseFeedbackSummary>(
      `${feedbackPath(opportunityId)}/summary`,
      { timeout: FEEDBACK_TIMEOUT },
    );
    return data ?? null;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404)
      return null;
    throw error;
  }
};
