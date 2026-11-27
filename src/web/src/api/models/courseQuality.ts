export type CourseQualityStatus = "Verified" | "NeedsReview" | "NotVerified";

export type CourseQualityCriterion =
  | "ListingLive"
  | "LinkWorks"
  | "DatesAccurate"
  | "DataComplete"
  | "FormatIdentified"
  | "JobOpen"
  | "JobDetailsClear"
  | "JobFresh"
  | "NoScamSignals";

export type CourseFormat = "Unknown" | "Course" | "Webinar" | "Job";

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
  /** Null when the course has no grade yet. Never "Ungraded". */
  grade: CourseGrade | null;
  gradeLabel: string | null;
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
  /** Why the result is unsure, in plain words. Empty when confidence met the threshold. */
  reviewReasons: string[];
  grade: CourseGradeResult | null;
  feedback: CourseFeedbackSummary | null;
}

export interface CourseQualityReviewSearchFilter {
  reviewStatus: CourseQualityReviewStatus | null;
  organizationId: string | null;
  grade?: CourseGrade | null;
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

// ── Iteration 2: grades, the editable rubric, learner feedback ──────────────
// Mirrors CourseQualityGradeContract.cs. The orchestrator owns both files.

export type CourseGradeFactor =
  | "LearnerFeedback"
  | "SyllabusAvailable"
  | "ProviderReliability"
  | "Comprehensiveness"
  | "CourseLength"
  | "Completion"
  | "ListingIntegrity"
  | "Accessibility";

/** Job grade factors: graded with the job rubric and ranked only against other jobs. Never part of the course rubric. */
export type JobGradeFactor =
  | "PayTransparency"
  | "JobClarity"
  | "YouthFit"
  | "ListingSafety"
  | "PartnerReliability"
  | "ApplicantFeedback";

export type CourseGrade = "Ungraded" | "A" | "B" | "C" | "D";

export interface CourseGradeFactorResult {
  factor: CourseGradeFactor | JobGradeFactor;
  /** 0 to 100 before weighting. */
  score: number;
  /** Share of 100 across the factors. */
  weight: number;
  /** False when the factor fell back to its neutral score. */
  hasData: boolean;
  reason: string;
}

export interface CourseGradeResult {
  opportunityId: string;
  grade: CourseGrade;
  gradeLabel: string;
  score: number;
  /** Share of graded courses this one scores at or above. 100 is the best. */
  percentile: number;
  factors: CourseGradeFactorResult[];
  /** Rules that held the grade below what the score alone would give. */
  caps: string[];
  rubricVersion: string;
  dateGraded: string;
}

export interface CourseGradeBand {
  grade: CourseGrade;
  label: string;
  /** Cumulative top share this band reaches down to. A at 15 means the best 15%. */
  topPercent: number;
  minScore: number;
}

export interface CourseGradeFactorOptions {
  weight: number;
  neutralScore: number;
  parameters: Record<string, number>;
}

export interface CourseGradeRubric {
  version: string;
  factors: Record<CourseGradeFactor, CourseGradeFactorOptions>;
  /** Best first. The last band takes everything left. */
  bands: CourseGradeBand[];
  needsReviewMaxGrade: CourseGrade;
  aMinSyllabusScore: number;
  aMinFeedbackScore: number;
  aFeedbackFloorMinResponses: number;
  minFactorsWithData: number;
}

export interface CourseGradeRubricState {
  rubric: CourseGradeRubric;
  isDefault: boolean;
  updatedByEmail: string | null;
  dateUpdated: string | null;
  distribution: Partial<Record<CourseGrade, number>>;
}

export interface CourseGradeFactorRow {
  opportunityId: string;
  opportunityTitle: string;
  organizationName: string;
  status: CourseQualityStatus;
  grade: CourseGrade;
  score: number;
  factors: Record<CourseGradeFactor, number>;
  factorsWithData: CourseGradeFactor[];
  /** Learners who answered that they took the course. */
  feedbackResponses: number;
}

export type CourseFeedbackAnswer =
  | "Pending"
  | "Taken"
  | "NotYet"
  | "NotTaken"
  | "Dismissed";

export interface CourseFeedbackPrompt {
  opportunityId: string;
  opportunityTitle: string;
  organizationName: string;
  organizationLogoURL: string | null;
  dateLeft: string;
}

export interface CourseFeedbackSubmission {
  answer: CourseFeedbackAnswer;
  rating: number | null;
  completed: boolean | null;
  wouldRecommend: boolean | null;
  matchedDescription: boolean | null;
  good: string[];
  bad: string[];
  /** At most 500 characters. Yoma reviewers only. */
  comment: string | null;
}

export interface CourseFeedbackTag {
  key: string;
  label: string;
}

export interface CourseFeedbackOptions {
  good: CourseFeedbackTag[];
  bad: CourseFeedbackTag[];
}

export interface CourseFeedbackTagCount {
  key: string;
  label: string;
  count: number;
}

export interface CourseFeedbackSummary {
  opportunityId: string;
  responses: number;
  completed: number;
  averageRating: number | null;
  adjustedRating: number | null;
  wouldRecommendYes: number;
  wouldRecommendNo: number;
  matchedDescriptionYes: number;
  matchedDescriptionNo: number;
  good: CourseFeedbackTagCount[];
  bad: CourseFeedbackTagCount[];
  /** Admin only: empty for anyone else. */
  recentComments: string[];
}
