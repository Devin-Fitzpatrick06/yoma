import type { StepDropOff } from "./pathway";
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
  aiUsage?: CourseQualityAIUsage | null; // iteration 5: the AI call behind this assessment, if any
  // drop-off reserved for the pilot: always Collecting for now (iteration 4, W5)
  dropOff?: StepDropOff;
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
  | "Accessibility"
  // weight 0 until there is data: what completers did next, and drop-off from the pilot
  | "Outcomes"
  | "DropOff";

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

// ── Iteration 4: pre-publish check (W1) ──────────────────────────────────────
// POST /courseQuality/organization/{organizationId}/draft/check   OrganisationAdmin of that organisation, Admin
//   body: the listing as the partner form would send it (OpportunityRequestBase) -> DraftCheckResult
//   404 unless the API switch CourseQuality:PrePublish:Enabled is on. Nothing is stored.

export type DraftCheckVerdict = "Verified" | "NeedsReview" | "NotVerified";

// Where a fix belongs on the partner form, so the panel can jump to the field.
export type DraftCheckField =
  | "title"
  | "summary"
  | "description"
  | "url"
  | "dateStart"
  | "dateEnd"
  | "commitment"
  | "skills"
  | "languages"
  | "countries"
  | "difficulty"
  | "other";

export interface DraftCheckFix {
  criterion: string; // the check's name, e.g. "LinkWorks", "DatesAccurate", "JobDetailsClear"
  field: DraftCheckField;
  hardGate: boolean; // true when this alone keeps the listing from being verified
  reason: string; // partner wording
  recommendation: string | null;
}

export interface DraftCheckResult {
  opportunityType: string; // Learning, Event, Job, ImpactAction
  predictedStatus: DraftCheckVerdict;
  score: number; // 0–100
  checks: { criterion: string; score: number; passed: boolean }[];
  fixes: DraftCheckFix[];
  linkChecked: boolean; // false when the link could not be confirmed while typing; never a hard fail
  summary: string; // one sentence, e.g. "Two fixes and this course will be verified."
  // "Draft this for me" (W1 phase 2): null unless the AI provider is configured
  suggestions: { field: DraftCheckField; value: string }[] | null;
}

// ── Iteration 5: live AI with usage and cost, swappable model, link health ───
// Contract for the BBS "Ziqi priority 1" asks. The C# models mirror these field for field.

/** Which model service ran: Anthropic's API, or any OpenAI-compatible server (Ollama, vLLM, LM Studio, a government host). */
export type AIProviderKind = "None" | "Anthropic" | "OpenAICompatible";

/** One AI call behind an assessment: what ran, what it used and what it cost. */
export interface CourseQualityAIUsage {
  provider: AIProviderKind;
  model: string;
  endpointHost: string | null; // e.g. "localhost:11434" for a local model; null for Anthropic
  isLocal: boolean; // served by the operator's own host: no per-token charge
  inputTokens: number;
  outputTokens: number;
  costUsd: number | null; // null when no price is configured for a remote model; 0 for local
  durationMs: number;
  dateRun: string;
  outcome: "Answered" | "Declined" | "Failed" | "Invalid"; // Declined = refusal / cut off; Invalid = reply not in the schema
}

// CourseQualityAssessment gains: aiUsage?: CourseQualityAIUsage | null  (null when no AI ran)

/** GET /courseQuality/ai/usage (Admin): the configured model and what AI runs have used. */
export interface CourseQualityAIUsageSummary {
  provider: AIProviderKind;
  model: string | null;
  endpointHost: string | null;
  isLocal: boolean;
  configured: boolean; // a provider is set and reachable configuration exists (key or base URL)
  runs: number;
  answered: number;
  inputTokens: number;
  outputTokens: number;
  costUsd: number; // sum of known costs
  unpricedRuns: number; // stored runs (history included) with an unknown cost, left out of costUsd
  remoteRuns: number; // stored runs (history included) on a model that was not local
  averageDurationMs: number | null;
  recent: CourseQualityAIRun[]; // newest first, at most 20
}

export interface CourseQualityAIRun {
  assessmentId: string;
  opportunityId: string;
  opportunityTitle: string;
  organizationId: string;
  usage: CourseQualityAIUsage;
}

// POST /courseQuality/links/check (Admin): link health for URLs from any platform (Yoma, YouthHub, 1MiO, …).
export interface LinkHealthRequest {
  urls: string[]; // at most 200 per request; duplicates checked once
  platform?: string | null; // free text label carried into the report, e.g. "YouthHub"
}

export type LinkHealthStatus =
  | "Ok" // loaded, looks like a real page
  | "Redirected" // loaded after redirects to another address
  | "NotFound" // 404 / 410, or a page that says it is gone
  | "Broken" // other 4xx/5xx, connection or TLS failure
  | "Blocked" // bot protection or sign-in wall: could not confirm (not counted as dead)
  | "Timeout"
  | "Invalid" // not an http(s) URL
  | "Refused"; // private, local or otherwise unsafe target: never fetched

export interface LinkHealthResult {
  url: string;
  status: LinkHealthStatus;
  httpStatus: number | null;
  finalUrl: string | null;
  durationMs: number | null;
  reason: string; // plain words
}

export interface LinkHealthReport {
  platform: string | null;
  checked: number;
  counts: Record<LinkHealthStatus, number>;
  results: LinkHealthResult[];
  dateChecked: string;
}
