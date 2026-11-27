import type { PathwayQuestion } from "~/api/models/pathway";
import { isAnywhereKey, type OnboardingAnswers } from "~/lib/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// My Yoma first visit (iteration 4, W7 and W3): which questions to ask, the "we used your Yoma
// preferences" line, and when the home page sends a youth to My Yoma. Pure, so it is unit tested.
// ─────────────────────────────────────────────────────────────────────────────

/** How long "Building your path" shows at least, unless skipped or reduced motion is on. */
export const BUILDING_MIN_MS = 1500;

/** Session storage key: the home page sent this browser to My Yoma once already this session. */
export const FIRST_VISIT_REDIRECT_KEY = "myYoma.firstVisitRedirected";

/** The intro's "Go to Yoma home" link: the home page never redirects with this query. */
export const YOMA_HOME_HREF = "/?home=1";

/** Plain words for the keys in `answeredFromPreferences`; question ids and preference fields. */
const PREFERENCE_LABELS: Record<string, string> = {
  goal: "goal",
  background: "starting point",
  knownSkills: "skills",
  skills: "skills",
  hoursPerWeek: "time",
  time: "time",
  timeCommitment: "time",
  country: "country",
  languages: "languages",
  categories: "interests",
  engagementTypes: "ways of taking part",
};

/** "time, languages, skills": each label once, in the API's order; `null` when none. */
export const preferencesSummary = (keys: string[]): string | null => {
  const labels: string[] = [];
  for (const key of keys) {
    const label = PREFERENCE_LABELS[key] ?? key.trim();
    if (label && !labels.includes(label)) labels.push(label);
  }
  return labels.length > 0 ? labels.join(", ") : null;
};

/**
 * The answer a hidden question gets when the youth was not asked it: nothing for skills (the API
 * reads them from preferences), "anywhere" for the country, the middle option for the time.
 * `null` when there is no neutral answer (goal, starting point): such a question is always asked.
 */
const fallbackAnswer = (question: PathwayQuestion): string[] | null => {
  if (question.multiSelect) return [];
  if (question.id === "country") {
    const anywhere = question.options.find((o) => isAnywhereKey(o.key));
    return anywhere ? [anywhere.key] : null;
  }
  if (question.id === "hoursPerWeek" && question.options.length > 0) {
    const middle = question.options[Math.floor(question.options.length / 2)];
    return middle ? [middle.key] : null;
  }
  return null;
};

/** Whether a question can stay hidden: answered from preferences and still answerable. */
const canHide = (
  question: PathwayQuestion,
  fromPreferences: Set<string>,
  answers: OnboardingAnswers,
): boolean =>
  fromPreferences.has(question.id) &&
  (answers[question.id].length > 0 || fallbackAnswer(question) !== null);

/**
 * The slides to show: every question not answered from Yoma's preferences, in the API's order.
 * `showAll` (the "edit" link) brings the hidden ones back. A question the API lists as answered
 * but that has neither a saved answer nor a neutral one (the goal, the starting point) is asked.
 */
export const questionsToAsk = (
  questions: PathwayQuestion[],
  answeredFromPreferences: string[],
  answers: OnboardingAnswers,
  showAll: boolean,
): PathwayQuestion[] => {
  if (showAll) return questions;
  const fromPreferences = new Set(answeredFromPreferences);
  return questions.filter((q) => !canHide(q, fromPreferences, answers));
};

/** The hidden questions' keys, for the "we used your preferences" line and the "edit" link. */
export const hiddenQuestionIds = (
  questions: PathwayQuestion[],
  answeredFromPreferences: string[],
  answers: OnboardingAnswers,
): PathwayQuestion["id"][] => {
  const fromPreferences = new Set(answeredFromPreferences);
  return questions
    .filter((q) => canHide(q, fromPreferences, answers))
    .map((q) => q.id);
};

/**
 * The answers to save: what the youth chose, plus a neutral answer for each hidden question left
 * empty, so the request is complete. The API prefers the stored preference for those (handoff).
 */
export const withPreferenceFallbacks = (
  answers: OnboardingAnswers,
  questions: PathwayQuestion[],
  answeredFromPreferences: string[],
): OnboardingAnswers => {
  const fromPreferences = new Set(answeredFromPreferences);
  const filled = { ...answers };
  for (const question of questions) {
    if (!fromPreferences.has(question.id) || filled[question.id].length > 0)
      continue;
    const fallback = fallbackAnswer(question);
    if (fallback) filled[question.id] = fallback;
  }
  return filled;
};

/** Where the question with `id` sits in `list`, or the nearest valid index. */
export const indexOfQuestion = (
  list: PathwayQuestion[],
  id: PathwayQuestion["id"] | undefined,
  fallback: number,
): number => {
  const found = id ? list.findIndex((q) => q.id === id) : -1;
  if (found >= 0) return found;
  return Math.max(0, Math.min(fallback, list.length - 1));
};

// ── First visit after sign-in ────────────────────────────────────────────────

export interface FirstVisitRedirectInput {
  /** `NEXT_PUBLIC_PATHWAY_ENABLED` */
  pathwayEnabled: boolean;
  sessionStatus: "authenticated" | "loading" | "unauthenticated";
  /** the session carries the Admin or OrganisationAdmin role: not a youth's first visit */
  isAdminOrPartner: boolean;
  /** Yoma's own profile check (`isUserProfileCompleted`); `null` while the profile loads */
  profileCompleted: boolean | null;
  /** Yoma's settings dialog would open (settings not configured and not skipped) */
  settingsDialogPending: boolean;
  /** this browser session was already sent to My Yoma, or chose "Go to Yoma home" */
  alreadyHandled: boolean;
  /** the youth's pathway profile: `true` saved, `false` none (404), `null` loading or failed */
  hasPathway: boolean | null;
}

/**
 * Whether the home page sends the youth to My Yoma. Only once per browser session, only for a
 * signed-in youth without a path, and only after Yoma's own profile and settings dialogs are done,
 * so the redirect never fights them and never loops.
 */
export const shouldRedirectToMyYoma = (
  input: FirstVisitRedirectInput,
): boolean =>
  input.pathwayEnabled &&
  input.sessionStatus === "authenticated" &&
  !input.isAdminOrPartner &&
  input.profileCompleted === true &&
  !input.settingsDialogPending &&
  !input.alreadyHandled &&
  input.hasPathway === false;

/** `/my-yoma-preview`: in the demo build always; elsewhere only in development with the switch on. */
export const isPreviewAllowed = (input: {
  production: boolean;
  demo: boolean;
  pathwayEnabled: boolean;
}): boolean => input.demo || (!input.production && input.pathwayEnabled);
