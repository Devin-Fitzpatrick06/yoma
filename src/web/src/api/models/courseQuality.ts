export type CourseQualityStatus = "Verified" | "NeedsReview" | "NotVerified";

export type CourseQualityCriterion =
  | "ListingLive"
  | "LinkWorks"
  | "DatesAccurate"
  | "DataComplete"
  | "FormatIdentified";

export type CourseFormat = "Unknown" | "Course" | "Webinar";

export type CourseQualityReviewStatus =
  | "NotRequired"
  | "Pending"
  | "Confirmed"
  | "Overridden";

export type CourseCostType = "Unknown" | "Free" | "Paid";

export interface CourseQualityCriterionResult {
  criterion: CourseQualityCriterion;
  score: number;
  hardGateFailed: boolean;
  reason: string;
  recommendation: string | null;
}

export interface LearnerCardCost {
  type: CourseCostType;
  amount: number | null;
  currency: string | null;
  evidence: string | null;
}

export interface LearnerCard {
  opportunityId: string;
  status: CourseQualityStatus;
  format: CourseFormat;
  dateStart: string;
  dateEnd: string | null;
  datesConfirmed: boolean;
  selfPaced: boolean;
  commitmentHours: number | null;
  timelineSummary: string | null;
  cost: LearnerCardCost;
  highlights: string[];
  dateAssessed: string;
}

export interface PartnerFixNote {
  assessmentId: string;
  opportunityId: string;
  opportunityTitle: string;
  status: CourseQualityStatus;
  score: number;
  summary: string;
  issues: CourseQualityCriterionResult[];
  dateAssessed: string;
}

export interface CourseQualityAssessment {
  id: string;
  opportunityId: string;
  opportunityTitle: string;
  organizationId: string;
  organizationName: string;
  rubricVersion: string;
  score: number;
  confidence: number;
  status: CourseQualityStatus;
  reviewStatus: CourseQualityReviewStatus;
  criteria: CourseQualityCriterionResult[];
  aiUsed: boolean;
  aiModel: string | null;
  aiSummary: string | null;
  learnerCard: LearnerCard;
  fixNote: PartnerFixNote | null;
  reviewedByEmail: string | null;
  reviewNote: string | null;
  dateReviewed: string | null;
  dateAssessed: string;
}

export interface CourseQualityReviewSearchFilter {
  reviewStatus: CourseQualityReviewStatus | null;
  organizationId: string | null;
  pageNumber: number;
  pageSize: number;
}

export interface CourseQualityReviewSearchResults {
  totalCount: number;
  items: CourseQualityAssessment[];
}

export interface CourseQualityReviewDecision {
  status: CourseQualityStatus;
  format: CourseFormat | null;
  note: string | null;
}

export interface CourseQualityCoverage {
  totalCourses: number;
  assessed: number;
  notAssessed: number;
  missingOrSuspectDates: number;
  course: number;
  webinar: number;
  unknown: number;
  cardReady: number;
}
