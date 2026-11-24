import { useEffect, useRef, useState } from "react";
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

// ─────────────────────────────────────────────────────────────────────────────
// My Yoma onboarding: one question per screen, a progress bar, Back and Next, and "Build my path"
// on the last screen. Options are toggle buttons (aria-pressed); the known-skills question takes
// several. "Change my answers" opens it again with the saved answers.
// ─────────────────────────────────────────────────────────────────────────────

const PathwayOnboarding: React.FC<{
  questions: PathwayQuestion[];
  initialAnswers: OnboardingAnswers;
  onSubmit: (request: PathwayProfileRequest) => void;
  isSaving?: boolean;
  /** shown when the youth already has a path, to go back to it unchanged */
  onCancel?: () => void;
  /** a save failure, shown above the buttons */
  error?: React.ReactNode;
}> = ({ questions, initialAnswers, onSubmit, isSaving, onCancel, error }) => {
  const [step, setStep] = useState(0);
  const [answers, setAnswers] = useState(initialAnswers);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const firstRender = useRef(true);

  // move focus to the new question, so screen readers announce it (not on first load)
  useEffect(() => {
    if (firstRender.current) {
      firstRender.current = false;
      return;
    }
    headingRef.current?.focus();
  }, [step]);

  const question = questions[step];
  if (!question) return null;

  const isLast = step === questions.length - 1;
  const request = toProfileRequest(answers);
  const nextEnabled =
    canProceed(answers, question) && (!isLast || request !== null);
  const selected = answers[question.id];
  const hintId = `pathway-hint-${question.id}`;

  const onNext = () => {
    if (!nextEnabled) return;
    if (!isLast) setStep(step + 1);
    else if (request) onSubmit(request);
  };

  return (
    <section
      aria-labelledby="pathway-question"
      className="mx-auto w-full max-w-2xl rounded-xl bg-white p-5 shadow md:p-8"
    >
      <div className="flex items-center justify-between gap-2">
        <p className="text-gray-dark text-xs font-semibold tracking-wider uppercase">
          Question {step + 1} of {questions.length}
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
      <progress
        className="progress progress-primary bg-gray mt-2 mb-6 h-1.5 w-full"
        value={step + 1}
        max={questions.length}
        aria-label={`Question ${step + 1} of ${questions.length}`}
      />

      <h2
        id="pathway-question"
        ref={headingRef}
        tabIndex={-1}
        className="font-nunito text-xl font-black text-black outline-none md:text-2xl"
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
          return (
            <button
              key={option.key}
              type="button"
              aria-pressed={pressed}
              onClick={() =>
                setAnswers(toggleOption(answers, question, option.key))
              }
              className={`focus-visible:outline-purple flex min-h-12 items-start gap-3 rounded-lg border-2 px-3.5 py-3 text-left transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 ${
                pressed
                  ? "border-purple bg-purple-tint"
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
                <span className="font-semibold text-black">{option.label}</span>
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

      {error && (
        <div role="alert" className="mt-5">
          {error}
        </div>
      )}

      <div className="mt-6 flex flex-wrap justify-between gap-2.5">
        <button
          type="button"
          className={BTN_SECONDARY}
          onClick={() => setStep(step - 1)}
          disabled={step === 0 || isSaving}
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
