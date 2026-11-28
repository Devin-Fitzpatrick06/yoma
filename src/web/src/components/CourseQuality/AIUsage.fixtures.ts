import type {
  CourseQualityAIUsage,
  CourseQualityAIUsageSummary,
  CourseQualityAssessment,
} from "~/api/models/courseQuality";

// Example AI runs for the fixture preview (`/ziqi-admin-preview`); not real usage or cost.

export const AI_USAGE_LOCAL_FIXTURE: CourseQualityAIUsage = {
  provider: "OpenAICompatible",
  model: "qwen2.5:3b",
  endpointHost: "localhost:11434",
  isLocal: true,
  inputTokens: 3214,
  outputTokens: 412,
  costUsd: 0,
  durationMs: 42_000,
  dateRun: "2026-11-28T09:14:00Z",
  outcome: "Answered",
};

export const AI_USAGE_REMOTE_PRICED_FIXTURE: CourseQualityAIUsage = {
  provider: "Anthropic",
  model: "claude-sonnet-4-5",
  endpointHost: null,
  isLocal: false,
  inputTokens: 9120,
  outputTokens: 925,
  costUsd: 0.0412,
  durationMs: 6_400,
  dateRun: "2026-11-27T15:02:00Z",
  outcome: "Answered",
};

export const AI_USAGE_REMOTE_UNPRICED_FIXTURE: CourseQualityAIUsage = {
  provider: "OpenAICompatible",
  model: "llama-3.1-8b-instruct",
  endpointHost: "ai.example.gov",
  isLocal: false,
  inputTokens: 2980,
  outputTokens: 0,
  costUsd: null,
  durationMs: 128_000,
  dateRun: "2026-11-27T11:40:00Z",
  outcome: "Invalid",
};

export const AI_USAGE_SUMMARY_FIXTURE: CourseQualityAIUsageSummary = {
  provider: "OpenAICompatible",
  model: "qwen2.5:3b",
  endpointHost: "localhost:11434",
  isLocal: true,
  configured: true,
  runs: 3,
  answered: 2,
  inputTokens: 9_604,
  outputTokens: 1_031,
  costUsd: 0,
  averageDurationMs: 51_300,
  recent: [
    {
      assessmentId: "fixture-assessment-1",
      opportunityId: "00000000-0000-0000-0000-000000000001",
      opportunityTitle: "Example: Introduction to data literacy",
      usage: AI_USAGE_LOCAL_FIXTURE,
    },
    {
      assessmentId: "fixture-assessment-2",
      opportunityId: "00000000-0000-0000-0000-000000000002",
      opportunityTitle: "Example: Careers in renewable energy (webinar)",
      usage: {
        ...AI_USAGE_LOCAL_FIXTURE,
        inputTokens: 3_870,
        outputTokens: 0,
        durationMs: 75_500,
        dateRun: "2026-11-28T08:51:00Z",
        outcome: "Failed",
      },
    },
    {
      assessmentId: "fixture-assessment-3",
      opportunityId: "00000000-0000-0000-0000-000000000003",
      opportunityTitle: "Example: Junior support technician",
      usage: {
        ...AI_USAGE_LOCAL_FIXTURE,
        inputTokens: 2_520,
        outputTokens: 619,
        durationMs: 36_400,
        dateRun: "2026-11-28T08:20:00Z",
      },
    },
  ],
};

export const AI_USAGE_EMPTY_FIXTURE: CourseQualityAIUsageSummary = {
  ...AI_USAGE_SUMMARY_FIXTURE,
  runs: 0,
  answered: 0,
  inputTokens: 0,
  outputTokens: 0,
  costUsd: 0,
  averageDurationMs: null,
  recent: [],
};

export const AI_USAGE_NOT_CONFIGURED_FIXTURE: CourseQualityAIUsageSummary = {
  ...AI_USAGE_EMPTY_FIXTURE,
  provider: "None",
  model: null,
  endpointHost: null,
  isLocal: false,
  configured: false,
};

/** An example assessment for the reviewer card's AI check section. */
export const aiCheckAssessmentFixture = (
  aiUsage: CourseQualityAIUsage | null,
  aiSummary: string | null,
): CourseQualityAssessment => ({
  aiUsage,
  id: `fixture-ai-${aiUsage?.model ?? "none"}-${aiUsage?.outcome ?? "none"}`,
  opportunityId: "00000000-0000-0000-0000-000000000001",
  opportunityTitle: "Example: Introduction to data literacy",
  organizationId: "00000000-0000-0000-0000-0000000000aa",
  organizationName: "Example learning partner",
  rubricVersion: "v4",
  score: 72,
  confidence: 0.64,
  status: "NeedsReview",
  reviewStatus: "Pending",
  criteria: [],
  aiUsed: aiUsage?.outcome === "Answered",
  aiModel: aiUsage?.model ?? "page-reader",
  aiSummary,
  learnerCard: {
    opportunityId: "00000000-0000-0000-0000-000000000001",
    status: "NeedsReview",
    format: "Course",
    dateStart: "2026-12-01T00:00:00Z",
    dateEnd: null,
    datesConfirmed: false,
    selfPaced: false,
    commitmentHours: 6,
    timelineSummary: null,
    cost: { type: "Free", amount: null, currency: null, evidence: null },
    highlights: [],
    dateAssessed: "2026-11-28T09:14:00Z",
    grade: null,
    gradeLabel: null,
  },
  fixNote: null,
  reviewedByEmail: null,
  reviewNote: null,
  dateReviewed: null,
  dateAssessed: "2026-11-28T09:14:00Z",
  reviewReasons: [],
  grade: null,
  feedback: null,
});

export const AI_SUMMARY_FIXTURE =
  "The page describes a free six-hour course in four modules. It gives a start date of 1 December but no end date, so the dates could not be confirmed.";
