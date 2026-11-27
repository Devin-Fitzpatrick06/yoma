import type { DraftCheckResult } from "~/api/models/courseQuality";
import type { PartnerBadge } from "~/api/models/pathway";

// Example Ziqi check answers for the preview page (`/ziqi-draft-preview`), without the API. Labelled
// example data: nothing here is a real partner or course.

export const DRAFT_CHECK_FIXTURES: Record<
  "notVerified" | "needsLook" | "verified",
  DraftCheckResult
> = {
  notVerified: {
    opportunityType: "Learning",
    predictedStatus: "NotVerified",
    score: 46,
    checks: [
      { criterion: "LinkWorks", score: 0, passed: false },
      { criterion: "DatesAccurate", score: 40, passed: false },
      { criterion: "DataComplete", score: 70, passed: true },
    ],
    fixes: [
      {
        criterion: "LinkWorks",
        field: "url",
        hardGate: true,
        reason: "The course link does not open a course page yet.",
        recommendation:
          "Paste the link learners use to start the course, beginning with https://.",
      },
      {
        criterion: "DatesAccurate",
        field: "dateStart",
        hardGate: false,
        reason: "The start date is in the past, so the course looks closed.",
        recommendation:
          "Set the next start date, or today if learners can start any time.",
      },
    ],
    linkChecked: false,
    summary: "Two fixes and this course will be verified.",
    suggestions: null,
  },
  needsLook: {
    opportunityType: "Learning",
    predictedStatus: "NeedsReview",
    score: 68,
    checks: [
      { criterion: "LinkWorks", score: 100, passed: true },
      { criterion: "DataComplete", score: 50, passed: false },
    ],
    fixes: [
      {
        criterion: "DataComplete",
        field: "summary",
        hardGate: false,
        reason: "The summary does not say what learners will be able to do.",
        recommendation:
          "Start with the skill, e.g. “Build a simple budget in a spreadsheet”.",
      },
      {
        criterion: "DataComplete",
        field: "skills",
        hardGate: false,
        reason: "No skills are listed.",
        recommendation: "Add two or three skills the course teaches.",
      },
    ],
    linkChecked: true,
    summary: "A reviewer will take a look. One more detail would help.",
    suggestions: [
      {
        field: "summary",
        value:
          "Learn to plan a monthly budget in a free spreadsheet, in four short lessons.",
      },
      { field: "dateStart", value: "Start any time" },
    ],
  },
  verified: {
    opportunityType: "Learning",
    predictedStatus: "Verified",
    score: 91,
    checks: [
      { criterion: "LinkWorks", score: 100, passed: true },
      { criterion: "DatesAccurate", score: 100, passed: true },
      { criterion: "DataComplete", score: 85, passed: true },
    ],
    fixes: [],
    linkChecked: true,
    summary: "This course will be verified when you publish it.",
    suggestions: [],
  },
};

const REVIEW_DUE = "2027-01-01T00:00:00Z";

export const PREVIEW_PARTNER_BADGES: PartnerBadge[] = [
  {
    organizationId: "00000000-0000-0000-0000-0000000000a1",
    organizationName: "Example Academy (demo)",
    level: "Platinum",
    gradedCourses: 32,
    waitingForReview: 1,
    shareTopGrades: 0.81,
    shareHeldBack: 0.01,
    minimumGradedCourses: 25,
    nextLevelHint: null,
    reviewDue: REVIEW_DUE,
  },
  {
    organizationId: "00000000-0000-0000-0000-0000000000a2",
    organizationName: "Example Learning Hub (demo)",
    level: "Gold",
    gradedCourses: 14,
    waitingForReview: 0,
    shareTopGrades: 0.64,
    shareHeldBack: 0.07,
    minimumGradedCourses: 5,
    nextLevelHint:
      "Platinum needs 75% of courses at A or B and 25 graded courses.",
    reviewDue: REVIEW_DUE,
  },
];
