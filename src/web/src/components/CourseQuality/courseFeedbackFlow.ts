import type {
  CourseFeedbackAnswer,
  CourseFeedbackSubmission,
} from "~/api/models/courseQuality";

// ─────────────────────────────────────────────────────────────────────────────
// The logic of the "Did you end up taking it?" prompt, kept free of React and the DOM so the
// unit tests (`pnpm test`, Node's runner) cover it: which routes may show it, how often it may
// check, the steps of the card and the payload each way out of it sends.
// ─────────────────────────────────────────────────────────────────────────────

/** At most one check for a due prompt per minute, however often the tab regains focus. */
export const FEEDBACK_CHECK_INTERVAL_MS = 60_000;

export const FEEDBACK_COMMENT_MAX = 500;

/**
 * Never on reviewer or partner pages (admin and organisation routes), nor on the sign-in
 * hand-off pages, so the prompt only ever meets learners browsing Yoma.
 */
const EXCLUDED_ROUTE_PREFIXES = ["/admin", "/organisations", "/auth", "/api"];

export const isFeedbackPromptRoute = (pathname: string): boolean => {
  const path = (pathname || "/").toLowerCase();
  return !EXCLUDED_ROUTE_PREFIXES.some(
    (prefix) => path === prefix || path.startsWith(`${prefix}/`),
  );
};

/** A simple "at most once per interval" gate. The first call always passes. */
export const createThrottle = (intervalMs = FEEDBACK_CHECK_INTERVAL_MS) => {
  let last: number | null = null;
  return {
    tryAcquire(now: number): boolean {
      if (last !== null && now - last < intervalMs) return false;
      last = now;
      return true;
    },
    reset() {
      last = null;
    },
  };
};

/** Anything that looks like an open dialog: Yoma's CustomModal, a native or DaisyUI one. */
export const ANOTHER_MODAL_SELECTOR =
  "#custom-modal-content, dialog[open], .modal.modal-open, .modal-toggle:checked";

export const isAnotherModalOpen = (
  doc: Pick<Document, "querySelector"> | null | undefined,
): boolean => {
  try {
    return !!doc?.querySelector(ANOTHER_MODAL_SELECTOR);
  } catch {
    return false;
  }
};

// ── the card ────────────────────────────────────────────────────────────────

export type FeedbackStep = "ask" | "rate" | "tags" | "thanks";

export interface FeedbackDraft {
  rating: number | null;
  completed: boolean | null;
  wouldRecommend: boolean | null;
  matchedDescription: boolean | null;
  good: string[];
  bad: string[];
  comment: string;
}

export const EMPTY_DRAFT: FeedbackDraft = {
  rating: null,
  completed: null,
  wouldRecommend: null,
  matchedDescription: null,
  good: [],
  bad: [],
  comment: "",
};

export interface FeedbackFlowState {
  step: FeedbackStep;
  /** The answer the thank-you is for, so it can say the right thing. */
  answer: CourseFeedbackAnswer | null;
  draft: FeedbackDraft;
}

export const INITIAL_FLOW_STATE: FeedbackFlowState = {
  step: "ask",
  answer: null,
  draft: EMPTY_DRAFT,
};

export type FeedbackFlowAction =
  | { type: "reset" }
  | { type: "yes" }
  | { type: "next" }
  | { type: "back" }
  | { type: "finished"; answer: CourseFeedbackAnswer }
  | { type: "setRating"; rating: number }
  | {
      type: "setAnswer";
      field: "completed" | "wouldRecommend" | "matchedDescription";
      value: boolean;
    }
  | { type: "toggleTag"; list: "good" | "bad"; key: string }
  | { type: "setComment"; comment: string };

const toggle = (list: string[], key: string) =>
  list.includes(key) ? list.filter((k) => k !== key) : [...list, key];

export const feedbackFlowReducer = (
  state: FeedbackFlowState,
  action: FeedbackFlowAction,
): FeedbackFlowState => {
  switch (action.type) {
    case "reset":
      return INITIAL_FLOW_STATE;
    case "yes":
      return { ...state, step: "rate", answer: "Taken" };
    case "next":
      return state.step === "rate" ? { ...state, step: "tags" } : state;
    case "back":
      return state.step === "tags" ? { ...state, step: "rate" } : state;
    case "finished":
      return { ...state, step: "thanks", answer: action.answer };
    case "setRating":
      return {
        ...state,
        draft: {
          ...state.draft,
          rating: Math.min(5, Math.max(1, Math.round(action.rating))),
        },
      };
    case "setAnswer":
      return {
        ...state,
        draft: { ...state.draft, [action.field]: action.value },
      };
    case "toggleTag":
      return {
        ...state,
        draft: {
          ...state.draft,
          [action.list]: toggle(state.draft[action.list], action.key),
        },
      };
    case "setComment":
      return {
        ...state,
        draft: {
          ...state.draft,
          comment: action.comment.slice(0, FEEDBACK_COMMENT_MAX),
        },
      };
    default:
      return state;
  }
};

/**
 * The request body for an answer. Only Taken carries the survey, with whatever was filled in
 * (so skipping still counts); every other answer sends empty survey fields.
 */
export const buildFeedbackSubmission = (
  answer: CourseFeedbackAnswer,
  draft: FeedbackDraft = EMPTY_DRAFT,
): CourseFeedbackSubmission => {
  if (answer !== "Taken")
    return {
      answer,
      rating: null,
      completed: null,
      wouldRecommend: null,
      matchedDescription: null,
      good: [],
      bad: [],
      comment: null,
    };

  const comment = draft.comment.trim().slice(0, FEEDBACK_COMMENT_MAX);
  return {
    answer,
    rating:
      draft.rating == null
        ? null
        : Math.min(5, Math.max(1, Math.round(draft.rating))),
    completed: draft.completed,
    wouldRecommend: draft.wouldRecommend,
    matchedDescription: draft.matchedDescription,
    good: [...new Set(draft.good)],
    bad: [...new Set(draft.bad)],
    comment: comment.length > 0 ? comment : null,
  };
};

export const THANKS_COPY: Partial<Record<CourseFeedbackAnswer, string>> = {
  Taken: "Thanks! This helps other learners choose.",
  NotYet: "No problem. We'll check in again later.",
  NotTaken: "Thanks for letting us know.",
  Dismissed: "Got it. We won't ask about this course again.",
};
