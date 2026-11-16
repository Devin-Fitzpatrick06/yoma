import axios from "axios";
import ApiClient from "~/lib/axiosClient";
import type {
  CourseGradeFactorRow,
  CourseGradeResult,
  CourseGradeRubric,
  CourseGradeRubricState,
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

// ── Iteration 2: grades and the editable rubric ─────────────────────────────

export const getCourseGradeRubric =
  async (): Promise<CourseGradeRubricState> => {
    const instance = await ApiClient;
    const { data } = await instance.get<CourseGradeRubricState>(
      "/courseQuality/grading/rubric",
    );
    return data;
  };

/** Saves the rubric as the admin override and regrades the catalogue. */
export const saveCourseGradeRubric = async (
  rubric: CourseGradeRubric,
): Promise<CourseGradeRubricState> => {
  const instance = await ApiClient;
  const { data } = await instance.put<CourseGradeRubricState>(
    "/courseQuality/grading/rubric",
    rubric,
  );
  return data;
};

/** Removes the override, so configuration applies again, and regrades. */
export const resetCourseGradeRubric =
  async (): Promise<CourseGradeRubricState> => {
    const instance = await ApiClient;
    const { data } = await instance.delete<CourseGradeRubricState>(
      "/courseQuality/grading/rubric",
    );
    return data;
  };

/** Every graded or gradeable course's factor scores, for regrading in the browser. */
export const getCourseGradeFactors = async (): Promise<
  CourseGradeFactorRow[]
> => {
  const instance = await ApiClient;
  const { data } = await instance.get<CourseGradeFactorRow[]>(
    "/courseQuality/grading/factors",
  );
  return data;
};

export const runCourseGrading = async (): Promise<CourseGradeRubricState> => {
  const instance = await ApiClient;
  const { data } = await instance.post<CourseGradeRubricState>(
    "/courseQuality/grading/run",
  );
  return data;
};

/** `null` when the course has no grade yet (404). */
export const getCourseGrade = async (
  opportunityId: string,
): Promise<CourseGradeResult | null> => {
  const instance = await ApiClient;
  try {
    const { data } = await instance.get<CourseGradeResult>(
      `/courseQuality/opportunity/${encodeURIComponent(opportunityId)}/grade`,
    );
    return data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404)
      return null;
    throw error;
  }
};
