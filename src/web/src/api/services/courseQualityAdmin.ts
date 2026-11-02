import ApiClient from "~/lib/axiosClient";
import type {
  CourseQualityAssessment,
  CourseQualityCoverage,
  CourseQualityReviewDecision,
  CourseQualityReviewSearchFilter,
  CourseQualityReviewSearchResults,
  PartnerFixNote,
} from "../models/courseQuality";

export const searchCourseQualityReviews = async (
  filter: CourseQualityReviewSearchFilter,
): Promise<CourseQualityReviewSearchResults> => {
  const instance = await ApiClient;
  const { data } = await instance.post<CourseQualityReviewSearchResults>(
    "/courseQuality/review/search",
    filter,
  );
  return data;
};

export const submitCourseQualityReview = async (
  assessmentId: string,
  decision: CourseQualityReviewDecision,
): Promise<CourseQualityAssessment> => {
  const instance = await ApiClient;
  const { data } = await instance.post<CourseQualityAssessment>(
    `/courseQuality/review/${encodeURIComponent(assessmentId)}`,
    decision,
  );
  return data;
};

export const assessCourseQuality = async (
  opportunityId: string,
): Promise<CourseQualityAssessment> => {
  const instance = await ApiClient;
  const { data } = await instance.post<CourseQualityAssessment>(
    `/courseQuality/opportunity/${encodeURIComponent(opportunityId)}/assess`,
  );
  return data;
};

export const getCourseQualityCoverage =
  async (): Promise<CourseQualityCoverage> => {
    const instance = await ApiClient;
    const { data } = await instance.get<CourseQualityCoverage>(
      "/courseQuality/coverage",
    );
    return data;
  };

export const getCourseQualityFixNotes = async (
  organizationId: string,
): Promise<PartnerFixNote[]> => {
  const instance = await ApiClient;
  const { data } = await instance.get<PartnerFixNote[]>(
    `/courseQuality/organization/${encodeURIComponent(organizationId)}/fixNotes`,
  );
  return data;
};
