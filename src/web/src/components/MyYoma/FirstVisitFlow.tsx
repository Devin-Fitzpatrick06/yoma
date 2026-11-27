import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import type {
  PathwayProfileRequest,
  PathwayQuestion,
} from "~/api/models/pathway";
import { BTN_PRIMARY } from "~/components/Common/buttonStyles";
import { toProfileRequest, type OnboardingAnswers } from "~/lib/pathway";
import {
  BUILDING_MIN_MS,
  YOMA_HOME_HREF,
  preferencesSummary,
  questionsToAsk,
  withPreferenceFallbacks,
} from "./firstVisit";
import { MyYomaMotionStyles, usePrefersReducedMotion } from "./MyYomaMotion";
import PathwayOnboarding from "./PathwayOnboarding";

// ─────────────────────────────────────────────────────────────────────────────
// The first visit to My Yoma, full screen: the YoID card flies in ("Your world, your Yoma"), then
// the question slides, then "Building your path" (about 1.5 s, skippable) until the path has
// loaded, then `onFinished` shows the map. "Change my answers" opens it at the slides, prefilled.
// Presentational: the page saves the answers and says when the path is ready.
// ─────────────────────────────────────────────────────────────────────────────

type Phase = "intro" | "questions" | "building";

/** What the intro card shows before a path exists: from the Yoma profile, not the pathway API. */
export interface IntroYoId {
  displayName: string | null;
  completedOpportunities: number | null;
}

const YoIdIntroCard: React.FC<{ yoId: IntroYoId }> = ({ yoId }) => (
  <div
    aria-hidden="true"
    className="myy-fly-in bg-purple relative mx-auto w-full max-w-sm overflow-hidden rounded-2xl p-5 text-white shadow-2xl ring-1 ring-white/20"
  >
    <span className="bg-yellow absolute -top-10 -right-10 h-28 w-28 rounded-full opacity-90" />
    <span className="bg-green absolute -bottom-12 -left-8 h-24 w-24 rounded-full opacity-60" />
    <p className="relative text-[11px] tracking-widest uppercase opacity-80">
      Your YoID
    </p>
    <p className="font-nunito relative mt-1 truncate text-2xl font-black">
      {yoId.displayName || "You"}
    </p>
    <div className="relative mt-6 flex items-end justify-between gap-3">
      <div>
        <p className="font-nunito text-3xl font-black tabular-nums">
          {(yoId.completedOpportunities ?? 0).toLocaleString("en")}
        </p>
        <p className="text-xs opacity-85">
          {yoId.completedOpportunities === 1
            ? "Opportunity completed"
            : "Opportunities completed"}
        </p>
      </div>
      <p className="font-nunito text-sm font-bold opacity-90">yoma</p>
    </div>
  </div>
);

const Intro: React.FC<{
  yoId: IntroYoId;
  summary: string | null;
  questionCount: number;
  onStart: () => void;
  isSaving?: boolean;
}> = ({ yoId, summary, questionCount, onStart, isSaving }) => (
  <section
    aria-labelledby="my-yoma-intro"
    className="mx-auto flex w-full max-w-2xl flex-col items-center gap-6 text-center text-white"
  >
    <YoIdIntroCard yoId={yoId} />
    <div className="myy-rise myy-delay-1 flex flex-col items-center gap-2">
      <h1
        id="my-yoma-intro"
        tabIndex={-1}
        className="font-nunito text-3xl font-black outline-none md:text-5xl"
      >
        Your world, your Yoma
      </h1>
      <p className="max-w-md text-sm opacity-90 md:text-base">
        {questionCount === 0
          ? "We have what we need from your Yoma preferences to line up courses, practice and jobs for you."
          : `${questionCount === 1 ? "One quick question" : `${questionCount} quick questions`}, then we line up courses, practice and jobs that lead to the work you want.`}
      </p>
      {summary && (
        <p className="text-xs opacity-80">
          We used your Yoma preferences for: {summary}.
        </p>
      )}
    </div>
    <div className="myy-rise myy-delay-2 flex flex-col items-center gap-3">
      <button
        type="button"
        onClick={onStart}
        disabled={isSaving}
        className="btn bg-yellow hover:bg-yellow/90 rounded-full border-0 px-8 text-base font-bold text-black normal-case focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
      >
        {questionCount === 0 ? "Build my path" : "Let's go"}
      </button>
      <Link
        href={YOMA_HOME_HREF}
        className="rounded text-sm underline underline-offset-2 opacity-90 hover:opacity-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
      >
        Go to Yoma home
      </Link>
    </div>
  </section>
);

const Building: React.FC<{
  onSkip: () => void;
  skipped: boolean;
  error?: React.ReactNode;
  onRetry?: () => void;
}> = ({ onSkip, skipped, error, onRetry }) => (
  <section
    aria-labelledby="my-yoma-building"
    className="mx-auto flex w-full max-w-md flex-col items-center gap-5 text-center text-white"
  >
    <svg
      viewBox="0 0 200 80"
      className="h-24 w-full max-w-xs"
      aria-hidden="true"
      fill="none"
    >
      <path
        d="M10 60 C 50 10, 80 70, 110 35 S 170 15, 190 30"
        pathLength={1}
        className="myy-draw stroke-yellow"
        strokeWidth={5}
        strokeLinecap="round"
      />
      <circle cx="10" cy="60" r="7" className="fill-white" />
      <circle cx="110" cy="35" r="6" className="fill-green-light" />
      <circle cx="190" cy="30" r="8" className="fill-yellow" />
    </svg>
    <h1
      id="my-yoma-building"
      tabIndex={-1}
      className="font-nunito text-2xl font-black outline-none md:text-4xl"
    >
      Building your path
    </h1>
    {error ? (
      <div role="alert" className="flex flex-col items-center gap-3">
        <div className="rounded-lg bg-white p-3 text-left text-black">
          {error}
        </div>
        {onRetry && (
          <button type="button" className={BTN_PRIMARY} onClick={onRetry}>
            Try again
          </button>
        )}
      </div>
    ) : (
      <>
        <p className="text-sm opacity-90" role="status">
          Lining up courses, practice and jobs for you
          <span aria-hidden="true" className="ml-1 inline-flex gap-1">
            <span className="myy-dot inline-block h-1.5 w-1.5 rounded-full bg-white" />
            <span className="myy-dot inline-block h-1.5 w-1.5 rounded-full bg-white" />
            <span className="myy-dot inline-block h-1.5 w-1.5 rounded-full bg-white" />
          </span>
        </p>
        {!skipped && (
          <button
            type="button"
            onClick={onSkip}
            className="rounded text-sm underline underline-offset-2 opacity-90 hover:opacity-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
          >
            Skip
          </button>
        )}
      </>
    )}
  </section>
);

const FirstVisitFlow: React.FC<{
  questions: PathwayQuestion[];
  answeredFromPreferences: string[];
  initialAnswers: OnboardingAnswers;
  /** "intro" on the first visit; "questions" for "Change my answers" */
  startAt: "intro" | "questions";
  yoId: IntroYoId;
  onSubmit: (request: PathwayProfileRequest) => void;
  isSaving: boolean;
  /** the save failed: back to the last slide with this message */
  saveError?: React.ReactNode;
  /** the saved path has loaded; the map can show */
  ready: boolean;
  /** the answers saved, but the path did not load */
  buildError?: React.ReactNode;
  onRetryBuild?: () => void;
  onFinished: () => void;
  onCancel?: () => void;
}> = ({
  questions,
  answeredFromPreferences,
  initialAnswers,
  startAt,
  yoId,
  onSubmit,
  isSaving,
  saveError,
  ready,
  buildError,
  onRetryBuild,
  onFinished,
  onCancel,
}) => {
  const [phase, setPhase] = useState<Phase>(startAt);
  const [minElapsed, setMinElapsed] = useState(false);
  const [skipped, setSkipped] = useState(false);
  const reducedMotion = usePrefersReducedMotion();
  const rootRef = useRef<HTMLDivElement>(null);
  const firstPhase = useRef(true);

  const askedCount = questionsToAsk(
    questions,
    answeredFromPreferences,
    initialAnswers,
    false,
  ).length;
  const summary =
    askedCount < questions.length
      ? preferencesSummary(answeredFromPreferences)
      : null;

  // a failed save goes back to the slides, where the error shows
  useEffect(() => {
    if (saveError && phase === "building") setPhase("questions");
  }, [saveError, phase]);

  // the minimum "building" time; none with reduced motion
  useEffect(() => {
    if (phase !== "building") return;
    setMinElapsed(false);
    setSkipped(false);
    const timer = window.setTimeout(
      () => setMinElapsed(true),
      reducedMotion ? 0 : BUILDING_MIN_MS,
    );
    return () => window.clearTimeout(timer);
  }, [phase, reducedMotion]);

  useEffect(() => {
    if (phase === "building" && ready && (minElapsed || skipped)) onFinished();
  }, [phase, ready, minElapsed, skipped, onFinished]);

  // focus the new phase's heading so screen readers follow; not on first load
  useEffect(() => {
    if (firstPhase.current) {
      firstPhase.current = false;
      return;
    }
    rootRef.current
      ?.querySelector<HTMLElement>(
        phase === "questions" ? "#pathway-question" : "h1",
      )
      ?.focus();
    window.scrollTo({ top: 0 });
  }, [phase]);

  const submit = (request: PathwayProfileRequest) => {
    setPhase("building");
    onSubmit(request);
  };

  const onStart = () => {
    if (askedCount > 0) {
      setPhase("questions");
      return;
    }
    const request = toProfileRequest(
      withPreferenceFallbacks(
        initialAnswers,
        questions,
        answeredFromPreferences,
      ),
    );
    if (request) submit(request);
    else setPhase("questions");
  };

  return (
    <div
      ref={rootRef}
      className="bg-purple relative isolate flex min-h-[100dvh] w-full flex-col items-center justify-center overflow-hidden px-4 pt-24 pb-10"
    >
      <MyYomaMotionStyles />
      {phase === "intro" && (
        <Intro
          yoId={yoId}
          summary={summary}
          questionCount={askedCount}
          onStart={onStart}
          isSaving={isSaving}
        />
      )}
      {phase === "questions" && (
        <div className="flex w-full flex-col items-center gap-4">
          <PathwayOnboarding
            questions={questions}
            answeredFromPreferences={answeredFromPreferences}
            initialAnswers={initialAnswers}
            onSubmit={submit}
            isSaving={isSaving}
            onCancel={onCancel}
            onBackFromFirst={
              startAt === "intro" ? () => setPhase("intro") : undefined
            }
            error={saveError}
          />
          {startAt === "intro" && (
            <Link
              href={YOMA_HOME_HREF}
              className="rounded text-sm text-white underline underline-offset-2 opacity-90 hover:opacity-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
            >
              Go to Yoma home
            </Link>
          )}
        </div>
      )}
      {phase === "building" && (
        <Building
          skipped={skipped}
          onSkip={() => setSkipped(true)}
          error={buildError}
          onRetry={onRetryBuild}
        />
      )}
    </div>
  );
};

export default FirstVisitFlow;
