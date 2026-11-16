import { useSession } from "next-auth/react";
import { useRouter } from "next/router";
import {
  useCallback,
  useEffect,
  useId,
  useReducer,
  useRef,
  useState,
} from "react";
import { IoMdClose } from "react-icons/io";
import { IoStar, IoStarOutline } from "react-icons/io5";
import type {
  CourseFeedbackAnswer,
  CourseFeedbackOptions,
  CourseFeedbackPrompt as CourseFeedbackPromptData,
} from "~/api/models/courseQuality";
import {
  getFeedbackOptions,
  getPendingFeedback,
  submitCourseFeedback,
} from "~/api/services/courseQualityLearner";
import { AvatarImage } from "~/components/AvatarImage";
import CustomModal from "~/components/Common/CustomModal";
import {
  FEEDBACK_COMMENT_MAX,
  INITIAL_FLOW_STATE,
  THANKS_COPY,
  buildFeedbackSubmission,
  createThrottle,
  feedbackFlowReducer,
  isAnotherModalOpen,
  isFeedbackPromptRoute,
} from "./courseFeedbackFlow";

// ─────────────────────────────────────────────────────────────────────────────
// "So, did you end up taking <course>?" — asked once when a signed-in learner comes back to Yoma
// after following a course link. Mounted once, app-wide, from Global.tsx. It waits for the page
// to settle, checks at most once a minute (on load, on navigation and when the tab is visible
// again), stays off admin and organisation pages and never stacks on another dialog.
// A bottom sheet on a phone, a small centred card from md up.
// ─────────────────────────────────────────────────────────────────────────────

// let the page's own requests go first
const FIRST_CHECK_DELAY_MS = 1500;
const THANKS_CLOSE_MS = 3500;
const HIDDEN_KEY = "yoma.courseFeedback.hidden";

// courses already answered or put off in this browser session, so a slow API cannot re-ask
const readHidden = (): string[] => {
  try {
    const raw = window.sessionStorage.getItem(HIDDEN_KEY);
    const list: unknown = raw ? JSON.parse(raw) : [];
    return Array.isArray(list) ? (list as string[]) : [];
  } catch {
    return [];
  }
};
const hideForSession = (opportunityId: string) => {
  try {
    const list = readHidden();
    if (!list.includes(opportunityId))
      window.sessionStorage.setItem(
        HIDDEN_KEY,
        JSON.stringify([...list, opportunityId].slice(-50)),
      );
  } catch {
    // private mode: the server's own state still prevents a repeat
  }
};

const PILL =
  "btn btn-sm h-11 min-h-11 rounded-full px-5 normal-case md:h-10 md:min-h-10";
const PILL_PRIMARY = `${PILL} bg-purple border-purple text-white hover:brightness-110`;
const PILL_SECONDARY = `${PILL} border-gray bg-white text-black hover:bg-gray-light`;
const LINK_BUTTON =
  "text-gray-dark rounded text-sm underline hover:text-black focus-visible:outline-2";

const YesNo: React.FC<{
  label: string;
  value: boolean | null;
  onChange: (value: boolean) => void;
}> = ({ label, value, onChange }) => {
  const labelId = useId();
  return (
    <div className="flex flex-col gap-2">
      <span id={labelId} className="text-sm font-semibold text-black">
        {label}
      </span>
      <div role="group" aria-labelledby={labelId} className="flex gap-2">
        {[true, false].map((option) => {
          const selected = value === option;
          return (
            <button
              key={String(option)}
              type="button"
              aria-pressed={selected}
              onClick={() => onChange(option)}
              className={`btn btn-sm h-11 min-h-11 flex-1 rounded-full normal-case md:h-10 md:min-h-10 ${
                selected
                  ? "bg-purple border-purple text-white"
                  : "border-gray hover:bg-gray-light bg-white text-black"
              }`}
            >
              {option ? "Yes" : "No"}
            </button>
          );
        })}
      </div>
    </div>
  );
};

const StarRating: React.FC<{
  value: number | null;
  onChange: (value: number) => void;
}> = ({ value, onChange }) => {
  const labelId = useId();
  return (
    <div className="flex flex-col gap-2">
      <span id={labelId} className="text-sm font-semibold text-black">
        How would you rate it?
      </span>
      <div
        role="radiogroup"
        aria-labelledby={labelId}
        className="flex justify-between gap-1 sm:justify-start"
      >
        {[1, 2, 3, 4, 5].map((star) => {
          const filled = value != null && star <= value;
          return (
            <button
              key={star}
              type="button"
              role="radio"
              aria-checked={value === star}
              aria-label={`${star} out of 5`}
              onClick={() => onChange(star)}
              className="hover:bg-gray-light flex h-12 w-12 items-center justify-center rounded-full focus-visible:outline-2"
            >
              {filled ? (
                <IoStar aria-hidden="true" className="text-yellow h-8 w-8" />
              ) : (
                <IoStarOutline
                  aria-hidden="true"
                  className="text-gray-dark h-8 w-8"
                />
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
};

const TagChips: React.FC<{
  label: string;
  tags: { key: string; label: string }[];
  selected: string[];
  onToggle: (key: string) => void;
}> = ({ label, tags, selected, onToggle }) => {
  const labelId = useId();
  if (tags.length === 0) return null;
  return (
    <div className="flex flex-col gap-2">
      <span id={labelId} className="text-sm font-semibold text-black">
        {label} <span className="text-gray-dark font-normal">(pick any)</span>
      </span>
      <div
        role="group"
        aria-labelledby={labelId}
        className="flex flex-wrap gap-2"
      >
        {tags.map((tag) => {
          const on = selected.includes(tag.key);
          return (
            <button
              key={tag.key}
              type="button"
              aria-pressed={on}
              onClick={() => onToggle(tag.key)}
              className={`badge h-auto min-h-9 rounded-full border px-3 py-1.5 text-sm ${
                on
                  ? "bg-purple border-purple text-white"
                  : "border-gray hover:bg-gray-light bg-white text-black"
              }`}
            >
              {tag.label}
            </button>
          );
        })}
      </div>
    </div>
  );
};

export const CourseFeedbackPrompt: React.FC = () => {
  const { status } = useSession();
  const router = useRouter();
  const allowedRoute = isFeedbackPromptRoute(router.pathname);
  const enabled = status === "authenticated" && allowedRoute;

  const [prompt, setPrompt] = useState<CourseFeedbackPromptData | null>(null);
  const [flow, dispatch] = useReducer(feedbackFlowReducer, INITIAL_FLOW_STATE);
  const [options, setOptions] = useState<CourseFeedbackOptions | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveFailed, setSaveFailed] = useState(false);

  const throttle = useRef(createThrottle());
  const checking = useRef(false);
  const openRef = useRef(false);
  openRef.current = prompt !== null;
  const returnFocus = useRef<HTMLElement | null>(null);
  const cardRef = useRef<HTMLDivElement>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const titleId = useId();
  const commentId = useId();
  const commentHintId = useId();

  const close = useCallback(() => {
    setPrompt(null);
    setSaving(false);
    setSaveFailed(false);
    dispatch({ type: "reset" });
    returnFocus.current?.focus?.();
    returnFocus.current = null;
  }, []);

  const check = useCallback(async () => {
    if (openRef.current || checking.current) return;
    if (document.visibilityState === "hidden") return;
    if (!throttle.current.tryAcquire(Date.now())) return;
    checking.current = true;
    try {
      const pending = await getPendingFeedback();
      const hidden = readHidden();
      const next = pending.find((p) => !hidden.includes(p.opportunityId));
      // never stack on another dialog; the next check (a minute on) tries again
      if (!next || openRef.current || isAnotherModalOpen(document)) return;
      returnFocus.current = document.activeElement as HTMLElement | null;
      dispatch({ type: "reset" });
      setPrompt(next);
    } finally {
      checking.current = false;
    }
  }, []);

  // a new sign-in starts with a fresh gate
  useEffect(() => {
    if (status !== "authenticated") throttle.current.reset();
  }, [status]);

  // on load and on each navigation (throttled), after the page has had a moment
  useEffect(() => {
    if (!enabled) return;
    const timer = window.setTimeout(() => void check(), FIRST_CHECK_DELAY_MS);
    return () => window.clearTimeout(timer);
  }, [enabled, router.asPath, check]);

  // coming back to the Yoma tab from the course's tab
  useEffect(() => {
    if (!enabled) return;
    const onVisible = () => {
      if (document.visibilityState === "visible") void check();
    };
    document.addEventListener("visibilitychange", onVisible);
    return () => document.removeEventListener("visibilitychange", onVisible);
  }, [enabled, check]);

  // leaving for an admin or organisation page (or signing out) closes it unanswered
  useEffect(() => {
    if (!enabled && prompt) close();
  }, [enabled, prompt, close]);

  // the survey's chips: fetched once a learner says yes, cached for the session
  useEffect(() => {
    if (flow.step !== "rate" || options) return;
    getFeedbackOptions()
      .then(setOptions)
      .catch(() => setOptions({ good: [], bad: [] }));
  }, [flow.step, options]);

  // move focus into the card on open and on each step, for keyboard and screen-reader users
  useEffect(() => {
    if (prompt) headingRef.current?.focus();
  }, [prompt, flow.step]);

  // the thank-you closes itself
  useEffect(() => {
    if (flow.step !== "thanks") return;
    const timer = window.setTimeout(close, THANKS_CLOSE_MS);
    return () => window.clearTimeout(timer);
  }, [flow.step, close]);

  const send = useCallback(
    async (answer: CourseFeedbackAnswer, { thank = true } = {}) => {
      if (!prompt || saving) return;
      const submission = buildFeedbackSubmission(answer, flow.draft);
      setSaving(true);
      setSaveFailed(false);
      try {
        await submitCourseFeedback(prompt.opportunityId, submission);
        hideForSession(prompt.opportunityId);
        if (thank) dispatch({ type: "finished", answer });
        else close();
      } catch {
        if (answer === "Taken" && thank) {
          // keep what they wrote so they can try again
          setSaveFailed(true);
        } else {
          // the course stays due on the server, so it is asked again another day
          hideForSession(prompt.opportunityId);
          if (thank) dispatch({ type: "finished", answer });
          else close();
        }
      } finally {
        setSaving(false);
      }
    },
    [prompt, saving, flow.draft, close],
  );

  // ✕: on the question it means "don't ask about this course again"; in the survey it skips
  // the rest but keeps the yes and whatever was filled in
  const onClose = useCallback(() => {
    if (flow.step === "thanks") close();
    else if (flow.step === "ask") void send("Dismissed", { thank: false });
    else void send("Taken", { thank: false });
  }, [flow.step, close, send]);

  // Escape is gentler than ✕: it puts the question off for this session without answering
  const onEscape = useCallback(() => {
    if (!prompt) return;
    if (flow.step === "ask") {
      hideForSession(prompt.opportunityId);
      close();
    } else onClose();
  }, [prompt, flow.step, close, onClose]);

  const onKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Escape") {
      event.stopPropagation();
      onEscape();
      return;
    }
    if (event.key !== "Tab" || !cardRef.current) return;
    // keep Tab inside the card while it is open
    const focusable = Array.from(
      cardRef.current.querySelectorAll<HTMLElement>(
        "button:not([disabled]), textarea, [href], [tabindex='0']",
      ),
    );
    if (focusable.length === 0) return;
    const first = focusable[0]!;
    const last = focusable[focusable.length - 1]!;
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  if (!prompt) return null;

  const { draft } = flow;
  const title = prompt.opportunityTitle;
  const closeLabel =
    flow.step === "ask"
      ? "Close and don't ask about this course again"
      : flow.step === "thanks"
        ? "Close"
        : "Skip the rest and close";

  return (
    <CustomModal
      isOpen={true}
      shouldCloseOnOverlayClick={false}
      onRequestClose={onEscape}
      animationStyle="slide-bottom"
      className="top-auto h-auto max-h-[90dvh] rounded-t-3xl md:top-0 md:h-fit md:max-h-[90vh] md:w-[440px]"
    >
      <div
        ref={cardRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onKeyDown={onKeyDown}
        className="flex flex-col gap-4 p-5 pb-[max(1.25rem,env(safe-area-inset-bottom))] md:p-6"
      >
        {/* HEADER: the course, and the way out */}
        <div className="flex items-start gap-3">
          <AvatarImage
            icon={prompt.organizationLogoURL ?? undefined}
            alt=""
            size={40}
          />
          <div className="min-w-0 flex-1">
            <p className="text-gray-dark truncate text-xs font-semibold">
              {prompt.organizationName}
            </p>
            <h2
              id={titleId}
              ref={headingRef}
              tabIndex={-1}
              className="font-nunito text-lg leading-snug font-black wrap-break-word text-black outline-none"
            >
              {flow.step === "ask" && <>So, did you end up taking {title}?</>}
              {flow.step === "rate" && <>Nice! How was {title}?</>}
              {flow.step === "tags" && <>Tell us a bit more</>}
              {flow.step === "thanks" && <>Thank you</>}
            </h2>
          </div>
          <button
            type="button"
            className="btn btn-sm btn-circle text-gray-dark hover:bg-gray-light h-11 min-h-11 w-11 shrink-0 border-none bg-transparent md:h-8 md:min-h-8 md:w-8"
            aria-label={closeLabel}
            onClick={onClose}
            disabled={saving}
          >
            <IoMdClose aria-hidden="true" className="h-5 w-5" />
          </button>
        </div>

        {/* STEP 0: the question */}
        {flow.step === "ask" && (
          <>
            <div className="flex flex-col gap-2 sm:flex-row">
              <button
                type="button"
                className={`${PILL_PRIMARY} sm:flex-1`}
                onClick={() => dispatch({ type: "yes" })}
                disabled={saving}
              >
                Yes
              </button>
              <button
                type="button"
                className={`${PILL_SECONDARY} sm:flex-1`}
                onClick={() => void send("NotYet")}
                disabled={saving}
              >
                Not yet
              </button>
              <button
                type="button"
                className={`${PILL_SECONDARY} sm:flex-1`}
                onClick={() => void send("NotTaken")}
                disabled={saving}
              >
                No
              </button>
            </div>
            <p className="text-gray-dark text-xs">
              Closing this with ✕ means we won&apos;t ask about this course
              again.
            </p>
          </>
        )}

        {/* STEP 1: rating, finished, recommend */}
        {flow.step === "rate" && (
          <>
            <p className="text-gray-dark text-xs">
              Step 1 of 2 · Every question is optional.
            </p>
            <StarRating
              value={draft.rating}
              onChange={(rating) => dispatch({ type: "setRating", rating })}
            />
            <YesNo
              label="Did you finish it?"
              value={draft.completed}
              onChange={(value) =>
                dispatch({ type: "setAnswer", field: "completed", value })
              }
            />
            <YesNo
              label="Would you recommend it to a friend?"
              value={draft.wouldRecommend}
              onChange={(value) =>
                dispatch({ type: "setAnswer", field: "wouldRecommend", value })
              }
            />
            <div className="flex items-center justify-between gap-3">
              <button
                type="button"
                className={LINK_BUTTON}
                onClick={() => void send("Taken")}
                disabled={saving}
              >
                Skip
              </button>
              <button
                type="button"
                className={PILL_PRIMARY}
                onClick={() => dispatch({ type: "next" })}
              >
                Next
              </button>
            </div>
          </>
        )}

        {/* STEP 2: tags, matched, comment */}
        {flow.step === "tags" && (
          <>
            <p className="text-gray-dark text-xs">
              Step 2 of 2 · Every question is optional.
            </p>
            {options === null ? (
              <span className="loading loading-dots loading-sm text-gray-dark">
                <span className="sr-only">Loading</span>
              </span>
            ) : (
              <>
                <TagChips
                  label="What was good?"
                  tags={options.good}
                  selected={draft.good}
                  onToggle={(key) =>
                    dispatch({ type: "toggleTag", list: "good", key })
                  }
                />
                <TagChips
                  label="What could be better?"
                  tags={options.bad}
                  selected={draft.bad}
                  onToggle={(key) =>
                    dispatch({ type: "toggleTag", list: "bad", key })
                  }
                />
              </>
            )}
            <YesNo
              label="Did it match what Yoma said about it?"
              value={draft.matchedDescription}
              onChange={(value) =>
                dispatch({
                  type: "setAnswer",
                  field: "matchedDescription",
                  value,
                })
              }
            />
            <div className="flex flex-col gap-1">
              <label
                htmlFor={commentId}
                className="text-sm font-semibold text-black"
              >
                Anything else?{" "}
                <span className="text-gray-dark font-normal">(optional)</span>
              </label>
              <textarea
                id={commentId}
                aria-describedby={commentHintId}
                className="textarea border-gray w-full rounded-lg text-base md:text-sm"
                rows={3}
                maxLength={FEEDBACK_COMMENT_MAX}
                value={draft.comment}
                onChange={(event) =>
                  dispatch({ type: "setComment", comment: event.target.value })
                }
              />
              <div
                id={commentHintId}
                className="text-gray-dark flex justify-between gap-3 text-xs"
              >
                <span>Only the Yoma team reads this.</span>
                <span aria-live="polite">
                  {draft.comment.length}/{FEEDBACK_COMMENT_MAX}
                </span>
              </div>
            </div>
            {saveFailed && (
              <p role="alert" className="text-sm text-black">
                That didn&apos;t send. Please check your connection and try
                again.
              </p>
            )}
            <div className="flex items-center justify-between gap-3">
              <button
                type="button"
                className={LINK_BUTTON}
                onClick={() => dispatch({ type: "back" })}
                disabled={saving}
              >
                Back
              </button>
              <button
                type="button"
                className={PILL_PRIMARY}
                onClick={() => void send("Taken")}
                disabled={saving}
              >
                {saving ? "Sending…" : "Send"}
              </button>
            </div>
          </>
        )}

        {saveFailed && flow.step === "rate" && (
          <p role="alert" className="text-sm text-black">
            That didn&apos;t send. Please check your connection and try again.
          </p>
        )}

        {/* THANKS */}
        {flow.step === "thanks" && (
          <div className="flex flex-col gap-3">
            <p role="status" className="text-sm text-black">
              {(flow.answer && THANKS_COPY[flow.answer]) ?? THANKS_COPY.Taken}
            </p>
            <button
              type="button"
              className={`${PILL_SECONDARY} self-end`}
              onClick={close}
            >
              Close
            </button>
          </div>
        )}
      </div>
    </CustomModal>
  );
};

export default CourseFeedbackPrompt;
