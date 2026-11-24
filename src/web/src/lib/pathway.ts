import type { CourseGrade } from "~/api/models/courseQuality";
import type {
  CourseOutcome,
  LearnerBadge,
  MyYoma,
  PartnerBadgeLevel,
  PathwayBackground,
  PathwayProfile,
  PathwayProfileRequest,
  PathwayQuestion,
} from "~/api/models/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// "My Yoma": the onboarding answers and the wording the page shows. Pure, so it is unit tested.
// ─────────────────────────────────────────────────────────────────────────────

export type PathwayQuestionId = PathwayQuestion["id"];

/** The selected option keys per question. Single-select questions hold at most one key. */
export type OnboardingAnswers = Record<PathwayQuestionId, string[]>;

export const emptyAnswers = (): OnboardingAnswers => ({
  goal: [],
  background: [],
  knownSkills: [],
  hoursPerWeek: [],
  country: [],
});

const BACKGROUNDS: PathwayBackground[] = [
  "InSchool",
  "FinishedSchool",
  "SomeStudy",
  "Degree",
];

export const BACKGROUND_LABELS: Record<PathwayBackground, string> = {
  InSchool: "Still in school",
  FinishedSchool: "Finished school",
  SomeStudy: "Some study or training",
  Degree: "University degree",
};

// The country question's "anywhere" option maps to `countryId: null`. The contract does not fix
// its key, so the usual spellings (and an empty key) are accepted.
const ANYWHERE_KEYS = ["", "anywhere", "worldwide", "other", "any", "null"];

export const isAnywhereKey = (key: string): boolean =>
  ANYWHERE_KEYS.includes(key.trim().toLowerCase());

/** Selects or clears an option: a multi-select toggles it, a single-select replaces the answer. */
export const toggleOption = (
  answers: OnboardingAnswers,
  question: Pick<PathwayQuestion, "id" | "multiSelect">,
  key: string,
): OnboardingAnswers => {
  const current = answers[question.id];
  const next = question.multiSelect
    ? current.includes(key)
      ? current.filter((k) => k !== key)
      : [...current, key]
    : [key];
  return { ...answers, [question.id]: next };
};

/** Whether the youth can move on: a multi-select may stay empty, a single-select needs one answer. */
export const canProceed = (
  answers: OnboardingAnswers,
  question: Pick<PathwayQuestion, "id" | "multiSelect">,
): boolean => question.multiSelect || answers[question.id].length === 1;

/** The request to save, or `null` while an answer is missing or not understood. */
export const toProfileRequest = (
  answers: OnboardingAnswers,
): PathwayProfileRequest | null => {
  const goalKey = answers.goal[0];
  const background = answers.background[0] as PathwayBackground | undefined;
  const hours = Number(answers.hoursPerWeek[0]);
  const country = answers.country[0];
  if (
    !goalKey ||
    !background ||
    !BACKGROUNDS.includes(background) ||
    !Number.isFinite(hours) ||
    hours <= 0 ||
    country === undefined
  )
    return null;
  return {
    goalKey,
    background,
    knownSkillKeys: [...answers.knownSkills],
    hoursPerWeek: hours,
    countryId: isAnywhereKey(country) ? null : country,
  };
};

/**
 * The saved answers as onboarding selections, so "Change my answers" opens prefilled. Keys the
 * questions no longer offer are dropped; a null country picks the question's "anywhere" option.
 */
export const answersFromProfile = (
  profile: PathwayProfileRequest | PathwayProfile,
  questions: PathwayQuestion[],
): OnboardingAnswers => {
  const offered = (id: PathwayQuestionId) =>
    new Set(
      questions.find((q) => q.id === id)?.options.map((o) => o.key) ?? [],
    );
  const keep = (id: PathwayQuestionId, keys: string[]) => {
    const options = offered(id);
    return keys.filter((k) => options.has(k));
  };
  const countryOptions = [...offered("country")];
  const country =
    profile.countryId === null
      ? countryOptions.filter(isAnywhereKey).slice(0, 1)
      : keep("country", [profile.countryId]);

  return {
    goal: keep("goal", [profile.goalKey]),
    background: keep("background", [profile.background]),
    knownSkills: keep("knownSkills", profile.knownSkillKeys),
    hoursPerWeek: keep("hoursPerWeek", [String(profile.hoursPerWeek)]),
    country,
  };
};

/** A 0–1 share as a whole percentage. */
export const percent = (share: number): number =>
  Math.round(Math.min(1, Math.max(0, share)) * 100);

/**
 * The outcome lines under a course, e.g. "62% went on to another course". Only rates the API
 * returned (it hides them below the minimum group size); never a comparison such as "2x".
 */
export const outcomeLines = (
  outcome: CourseOutcome | null | undefined,
): string[] => {
  if (!outcome) return [];
  const lines: string[] = [];
  if (outcome.progressionRate !== null)
    lines.push(
      `${percent(outcome.progressionRate)}% went on to another course`,
    );
  if (outcome.employmentRate !== null)
    lines.push(
      `${percent(outcome.employmentRate)}% later completed a Yoma job`,
    );
  return lines;
};

const plural = (n: number, one: string, many = `${one}s`) =>
  `${n.toLocaleString("en")} ${n === 1 ? one : many}`;

/** "10 courses · about 64 hours · roughly 13 weeks at 5 h a week" */
export const goalMeta = (
  myYoma: Pick<
    MyYoma,
    "totalCourses" | "totalHours" | "estimatedWeeks" | "profile"
  >,
): string =>
  [
    plural(myYoma.totalCourses, "course"),
    `about ${plural(Math.round(myYoma.totalHours), "hour")}`,
    `roughly ${plural(myYoma.estimatedWeeks, "week")} at ${myYoma.profile.hoursPerWeek} h a week`,
  ].join(" · ");

/** The share of the path completed, 0–100. */
export const pathProgress = (
  myYoma: Pick<MyYoma, "completedCourses" | "totalCourses">,
): number =>
  myYoma.totalCourses > 0
    ? percent(myYoma.completedCourses / myYoma.totalCourses)
    : 0;

/** "Next: learn how marketing works", or `null` for the last stage. */
export const nextStageLabel = (leadsTo: string | null): string | null => {
  const next = leadsTo?.trim();
  if (!next) return null;
  return `Next: ${next.charAt(0).toLowerCase()}${next.slice(1)}`;
};

const GRADES: CourseGrade[] = ["A", "B", "C", "D"];

/** The contract carries the grade as a string; only A–D render a chip. */
export const asCourseGrade = (grade: string | null): CourseGrade | null =>
  GRADES.includes(grade as CourseGrade) ? (grade as CourseGrade) : null;

/** Progress towards a badge not yet earned, 0–100; `null` when earned or unknown. */
export const badgeProgress = (badge: LearnerBadge): number | null =>
  badge.earned || badge.progress === null ? null : percent(badge.progress);

/** Earned badges first, then the closest to earning. */
export const sortBadges = (badges: LearnerBadge[]): LearnerBadge[] =>
  [...badges].sort(
    (a, b) =>
      Number(b.earned) - Number(a.earned) ||
      (b.progress ?? 0) - (a.progress ?? 0),
  );

export const PARTNER_BADGE_LABELS: Record<PartnerBadgeLevel, string> = {
  Gold: "Gold partner",
  Silver: "Silver partner",
  Bronze: "Bronze partner",
  None: "No badge yet",
  NotEnoughGraded: "Not enough graded courses yet",
};
