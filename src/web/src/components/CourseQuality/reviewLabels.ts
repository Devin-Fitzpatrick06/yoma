import type {
  CourseFormat,
  CourseQualityAssessment,
  CourseQualityCriterion,
  CourseQualityReviewStatus,
  CourseQualityStatus,
} from "~/api/models/courseQuality";

// Labels shared by the reviewer queue and the click-through reviewer mode.

export type FinalStatus = Exclude<CourseQualityStatus, "NeedsReview">;

export const CRITERION_LABELS: Record<CourseQualityCriterion, string> = {
  ListingLive: "Listing is live",
  LinkWorks: "Link works",
  DatesAccurate: "Dates are accurate",
  DataComplete: "Data is complete",
  FormatIdentified: "Format is identified",
};

export const FORMAT_LABELS: Record<CourseFormat, string> = {
  Course: "Course",
  Webinar: "Webinar",
  Unknown: "Format not identified",
};

export const STATUS_LABELS: Record<CourseQualityStatus, string> = {
  Verified: "Verified",
  NeedsReview: "Needs review",
  NotVerified: "Not verified",
};

export const STATUS_BADGES: Record<CourseQualityStatus, string> = {
  Verified: "bg-green-light text-green",
  NeedsReview: "bg-yellow-tint text-yellow",
  NotVerified: "bg-orange-light text-orange",
};

export const REVIEW_STATUS_LABELS: Record<CourseQualityReviewStatus, string> = {
  Pending: "Pending",
  Confirmed: "Confirmed",
  Overridden: "Overridden",
  NotRequired: "Not required",
};

/** The confidence below which an assessment goes to review (CourseQuality:ReviewConfidenceThreshold). */
export const REVIEW_CONFIDENCE_THRESHOLD = 0.7;

export const getDecisionLabel = (
  status: CourseQualityStatus,
  target: FinalStatus,
): string => {
  const label = STATUS_LABELS[target];
  if (status === target) return `Confirm ${label}`;
  if (status === "NeedsReview") return `Mark ${label}`;
  return `Override to ${label}`;
};

/**
 * Events and jobs are checked without the course-only format check, so they carry no
 * FormatIdentified criterion and a reviewer verifies them without choosing a format.
 */
export const needsFormat = (item: CourseQualityAssessment): boolean =>
  item.criteria.some((c) => c.criterion === "FormatIdentified");
