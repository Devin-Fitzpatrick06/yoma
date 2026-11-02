import axios from "axios";
import type { GetServerSidePropsContext, GetStaticPropsContext } from "next";
import ApiClient from "~/lib/axiosClient";
import ApiServer from "~/lib/axiosServer";
import type { LearnerCard } from "../models/courseQuality";

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
