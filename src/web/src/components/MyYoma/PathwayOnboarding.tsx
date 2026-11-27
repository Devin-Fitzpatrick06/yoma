import { useEffect, useMemo, useRef, useState } from "react";
import { FaCheck } from "react-icons/fa";
import type {
  PathwayProfileRequest,
  PathwayQuestion,
} from "~/api/models/pathway";
import { BTN_PRIMARY, BTN_SECONDARY } from "~/components/Common/buttonStyles";
import {
  canProceed,
  toggleOption,
  toProfileRequest,
  type OnboardingAnswers,
} from "~/lib/pathway";
import {
  hiddenQuestionIds,
  indexOfQuestion,
  preferencesSummary,
  questionsToAsk,
  withPreferenceFallbacks,
} from "./firstVisit";

// ─────────────────────────────────────────────────────────────────────────────
// My Yoma questions as slides: one question per slide, a progress bar, Back and Next, and "Build
// my path" on the last slide. Only the questions Yoma's own preferences did not answer are asked;
// a line says which preferences were used, and "edit" brings those questions back. Options are
// toggle buttons (aria-pressed) that pop when chosen; the known-skills question takes several.
// ─────────────────────────────────────────────────────────────────────────────

type Direction = "next" | "prev";

const PathwayOnboarding: React.FC<{
  questions: PathwayQuestion[];
  /** keys of questions (and preference fields) answered from Yoma's preferences */
  answeredFromPreferences?: string[];
  initialAnswers: OnboardingAnswers;
  onSubmit: (request: PathwayProfileRequest) => void;
  isSaving?: boolean;
  /** shown when the youth already has a path, to go back to it unchanged */
  onCancel?: () => void;
  /** Back on the first slide (to the intro); without it Back is disabled there */
  onBackFromFirst?: () => void;
  /** a save failure, shown above the buttons */
  error?: React.ReactNode;
}> = ({
  questions,
  answeredFromPreferences = [],
  initialAnswers,
  onSubmit,
  isSaving,
  onCancel,
  onBackFromFirst,
  error,
}) => {
  const [answers, setAnswers] = useState(initialAnswers);
  const [showAll, setShowAll] = useState(false);
  const [step, setStep] = useState(0);
  const [direction, setDirection] = useState<Direction>("next");
  const [pop, setPop] = useState<{ key: string; n: number } | null>(null);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const firstRender = useRef(true);

  // which slides to show is fixed by the answers the flow started with, so a slide never
  // disappears while the youth is answering it
  const asked = useMemo(
    () =>
      questionsToAsk(
        questions,
        answeredFromPreferences,
        initialAnswers,
        showAll,
      ),
    [questions, answeredFromPreferences, initialAnswers, showAll],
  );
  const hidden = useMemo(
    () =>
      showAll
        ? []
        : hiddenQuestionIds(questions, answeredFromPreferences, initialAnswers),
    [questions, answeredFromPreferences, initialAnswers, showAll],
  );
  const summary =
    hidden.length > 0 ? preferencesSummary(answeredFromPreferences) : null;

  // move focus to the new question, so screen readers announce it (not on first load)
  useEffect(() => {
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    headingRef.current?.focus();
  }, [step, showAll]);

  const question = asked[step];
  if (!question) return null;

  const isLast = step === asked.length - 1;
  const request = toProfileRequest(
    withPreferenceFallbacks(answers, questions, answeredFromPreferences),
  );
  const nextEnabled =
    canProceed(answers, question) && (!isLast || request !== null);
  const selected = answers[question.id];
  const hintId = `pathway-hint-${question.id}`;
  const progress = Math.round(((step + 1) / asked.length) * 100);

  const goTo = (to: number, dir: Direction) => {
    setDirection(dir);
    setStep(to);
  };

  const onNext = () => {
    if (!nextEnabled) return;
    if (!isLast) goTo(step + 1, "next");
    else if (request) onSubmit(request);
  };

  const onBack = () => {
    if (step > 0) goTo(step - 1, "prev");
    else onBackFromFirst?.();
  };

  const onShowAll = () => {
    const all = questionsToAsk(
      questions,
      answeredFromPreferences,
      initialAnswers,
      true,
    );
    setDirection("next");
    setStep(indexOfQuestion(all, question.id, step));
    setShowAll(true);
  };

  const onChoose = (key: string) => {
    setAnswers(toggleOption(answers, question, key));
    setPop({ key, n: (pop?.n ?? 0) + 1 });
  };

  return (
    <section
      aria-labelledby="pathway-question"
      className="mx-auto flex w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-white p-5 shadow-xl md:p-8"
    >
      <div className="flex items-center justify-between gap-2">
        <p className="text-gray-dark text-xs font-semibold tracking-wider uppercase">
          Question {step + 1} of {asked.length}
        </p>
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            className="text-gray-dark focus-visible:outline-purple rounded text-xs underline hover:text-black focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            Keep my path
          </button>
        )}
      </div>
      <div
        role="progressbar"
        aria-label={`Question ${step + 1} of ${asked.length}`}
        aria-valuemin={1}
        aria-valuemax={asked.length}
        aria-valuenow={step + 1}
        className="bg-gray mt-2 mb-4 h-1.5 w-full overflow-hidden rounded-full"
      >
        <div
          className="myy-progress bg-purple h-full rounded-full"
          style={{ width: `${progress}%` }}
        />
      </div>

      {summary && (
        <p className="bg-green-light text-green mb-4 rounded-lg px-3 py-2 text-xs md:text-sm">
          We used your Yoma preferences for: {summary}.{" "}
          <button
            type="button"
            onClick={onShowAll}
            className="focus-visible:outline-purple rounded font-semibold underline underline-offset-2 hover:text-black focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            Edit
            <span className="sr-only"> these answers too</span>
          </button>
        </p>
      )}

      {/* the slide: keyed by question, so it slides in afresh on every step */}
      <div
        key={`${question.id}-${showAll ? "all" : "asked"}`}
        className={direction === "next" ? "myy-slide-next" : "myy-slide-prev"}
      >
        <h2
          id="pathway-question"
          ref={headingRef}
          tabIndex={-1}
          className="font-nunito text-xl font-black text-black outline-none md:text-3xl"
        >
          {question.title}
        </h2>
        <p id={hintId} className="text-gray-dark mt-1 mb-5 text-sm">
          {question.hint}
        </p>

        <div
          role="group"
          aria-labelledby="pathway-question"
          aria-describedby={hintId}
          className="grid grid-cols-1 gap-2.5 sm:grid-cols-2"
        >
          {question.options.map((option) => {
            const pressed = selected.includes(option.key);
            const popClass =
              pop?.key === option.key
                ? pop.n % 2 === 0
                  ? "myy-pop-a"
                  : "myy-pop-b"
                : "";
            return (
              <button
                key={option.key}
                type="button"
                aria-pressed={pressed}
                onClick={() => onChoose(option.key)}
                className={`focus-visible:outline-purple flex min-h-14 items-start gap-3 rounded-2xl border-2 px-4 py-3 text-left transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 ${popClass} ${
                  pressed
                    ? "border-purple bg-purple-tint shadow-md"
                    : "border-gray bg-gray-light hover:border-purple-light"
                }`}
              >
                <span
                  aria-hidden="true"
                  className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center border-2 ${
                    question.multiSelect ? "rounded" : "rounded-full"
                  } ${pressed ? "border-purple bg-purple text-white" : "border-gray-dark/40 bg-white"}`}
                >
                  {pressed && <FaCheck className="h-2.5 w-2.5" />}
                </span>
                <span className="flex min-w-0 flex-col">
                  <span className="font-semibold text-black">
                    {option.label}
                  </span>
                  {option.description && (
                    <span className="text-gray-dark text-sm">
                      {option.description}
                    </span>
                  )}
                </span>
              </button>
            );
          })}
        </div>
      </div>

      {error && (
        <div role="alert" className="mt-5">
          {error}
        </div>
      )}

      <div className="mt-6 flex flex-wrap justify-between gap-2.5">
        <button
          type="button"
          className={BTN_SECONDARY}
          onClick={onBack}
          disabled={(step === 0 && !onBackFromFirst) || isSaving}
        >
          Back
        </button>
        <button
          type="button"
          className={BTN_PRIMARY}
          onClick={onNext}
          disabled={!nextEnabled || isSaving}
        >
          {isLast
            ? isSaving
              ? "Building your path…"
              : "Build my path"
            : "Next"}
        </button>
      </div>
    </section>
  );
};

export default PathwayOnboarding;
