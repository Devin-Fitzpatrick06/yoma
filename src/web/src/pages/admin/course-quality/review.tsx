import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { type GetServerSidePropsContext } from "next";
import { getServerSession } from "next-auth";
import Head from "next/head";
import Link from "next/link";
import { useRouter } from "next/router";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactElement,
  type ReactNode,
} from "react";
import {
  IoArrowBack,
  IoCheckmarkCircle,
  IoCloseCircle,
  IoEllipseOutline,
  IoOpenOutline,
  IoRemoveCircle,
} from "react-icons/io5";
import Moment from "react-moment";
import { toast } from "react-toastify";
import type {
  CourseQualityAssessment,
  CourseQualityCriterionResult,
} from "~/api/models/courseQuality";
import {
  searchCourseQualityReviews,
  submitCourseQualityReview,
} from "~/api/services/courseQualityAdmin";
import {
  BTN_DANGER,
  BTN_PRIMARY,
  BTN_SECONDARY,
} from "~/components/Common/buttonStyles";
import CourseFeedbackLazySummary from "~/components/CourseQuality/CourseFeedbackLazySummary";
import CourseFeedbackSummaryPanel from "~/components/CourseQuality/CourseFeedbackSummaryPanel";
import { STICKY_ACTION_BAR } from "~/components/CourseQuality/DemoViewSwitcher";
import GradeBreakdown, {
  GradeCaps,
  WAITING_FOR_REVIEW_TEXT,
} from "~/components/CourseQuality/GradeBreakdown";
import GradeChip from "~/components/CourseQuality/GradeChip";
import ReviewDecisionDialog, {
  type PendingDecision,
  type ReviewDecisionInput,
} from "~/components/CourseQuality/ReviewDecisionDialog";
import {
  CRITERION_LABELS,
  type FinalStatus,
  FORMAT_LABELS,
  REVIEW_CONFIDENCE_THRESHOLD,
  STATUS_BADGES,
  STATUS_LABELS,
} from "~/components/CourseQuality/reviewLabels";
import MainLayout from "~/components/Layout/Main";
import { ApiErrors } from "~/components/Status/ApiErrors";
import { InternalServerError } from "~/components/Status/InternalServerError";
import { Unauthenticated } from "~/components/Status/Unauthenticated";
import { Unauthorized } from "~/components/Status/Unauthorized";
import { DATE_FORMAT_HUMAN, ROLE_ADMIN, THEME_BLUE } from "~/lib/constants";
import { fetchReviewBatch } from "~/lib/courseQualityReviewBatch";
import { type NextPageWithLayout } from "~/pages/_app";
import { authOptions } from "~/server/auth";

// ─────────────────────────────────────────────────────────────────────────────
// Reviewer mode: the pending queue one course at a time, like a card stack. Each card says why
// the course is here, what each check found, its grade and what learners said, and shows what
// the learner and the partner see. The batch is a snapshot, so deciding does not reshuffle it.
// ─────────────────────────────────────────────────────────────────────────────

const QUEUE_PATH = "/admin/course-quality";
const BATCH_SIZE = 50;
// its own root, so invalidating ["courseQuality"] after a decision leaves the batch alone
const SESSION_QUERY_KEY = ["courseQualityReviewSession"];

type Outcome = FinalStatus | "Skipped";

const OUTCOME_LABELS: Record<Outcome, string> = {
  Verified: "Verified",
  NotVerified: "Not verified",
  Skipped: "Skipped",
};

const isDecided = (outcome: Outcome | undefined) =>
  outcome === "Verified" || outcome === "NotVerified";

type CheckResult = "Pass" | "Partly" | "Fail";

const checkResultOf = (result: CourseQualityCriterionResult): CheckResult =>
  result.hardGateFailed || result.score < 50
    ? "Fail"
    : result.score >= 80
      ? "Pass"
      : "Partly";

const CHECK_RESULT_STYLES: Record<
  CheckResult,
  { Icon: React.ElementType; className: string }
> = {
  Pass: { Icon: IoCheckmarkCircle, className: "text-purple" },
  Partly: { Icon: IoRemoveCircle, className: "text-yellow" },
  Fail: { Icon: IoCloseCircle, className: "text-orange" },
};

const Section: React.FC<{
  title: string;
  aside?: ReactNode;
  children: ReactNode;
}> = ({ title, aside, children }) => (
  <section className="flex flex-col gap-2">
    <div className="flex flex-wrap items-baseline justify-between gap-2">
      <h3 className="font-nunito text-base font-black text-black">{title}</h3>
      {aside}
    </div>
    {children}
  </section>
);

const Kbd: React.FC<{ children: ReactNode }> = ({ children }) => (
  <kbd className="kbd kbd-xs text-gray-dark ml-1 hidden md:inline-flex">
    {children}
  </kbd>
);

const ConfidenceMeter: React.FC<{ confidence: number }> = ({ confidence }) => {
  const percent = Math.round(confidence * 100);
  const threshold = Math.round(REVIEW_CONFIDENCE_THRESHOLD * 100);
  const below = confidence < REVIEW_CONFIDENCE_THRESHOLD;
  return (
    <div className="flex flex-col gap-1">
      <div className="flex flex-wrap items-baseline justify-between gap-2 text-sm">
        <span>
          Confidence <span className="font-nunito font-black">{percent}%</span>
        </span>
        <span className="text-gray-dark text-xs">
          {below
            ? `${threshold - percent} ${threshold - percent === 1 ? "point" : "points"} under the ${threshold}% needed to decide on its own`
            : `At or above the ${threshold}% threshold`}
        </span>
      </div>
      <div
        role="meter"
        aria-label="Confidence"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
        aria-valuetext={`${percent}%, threshold ${threshold}%`}
        className="bg-gray-light relative h-2.5 w-full rounded-full"
      >
        <div
          className={`h-full rounded-full ${below ? "bg-yellow" : "bg-blue"}`}
          style={{ width: `${Math.max(0, Math.min(100, percent))}%` }}
        />
        <span
          aria-hidden="true"
          className="absolute -top-1 -bottom-1 w-0.5 rounded bg-black"
          style={{ left: `${threshold}%` }}
        />
      </div>
    </div>
  );
};

const ReviewCard: React.FC<{
  item: CourseQualityAssessment;
  headingRef: React.Ref<HTMLHeadingElement>;
}> = ({ item, headingRef }) => {
  // the search carries the grade; null means the course has none yet
  const grade = item.grade ?? null;
  const reasons = item.reviewReasons ?? [];
  const learnerHref = `/opportunities/${item.opportunityId}`;
  const partnerHref = `/organisations/${item.organizationId}/course-quality`;

  return (
    <article className="flex flex-col gap-6 rounded-2xl bg-white p-4 shadow-sm md:p-6">
      {/* TITLE */}
      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <span
            className={`badge border-none text-xs font-semibold ${STATUS_BADGES[item.status]}`}
          >
            {STATUS_LABELS[item.status]}
          </span>
          <span className="badge bg-gray-light text-gray-dark border-none text-xs font-semibold">
            {FORMAT_LABELS[item.learnerCard?.format] ?? FORMAT_LABELS.Unknown}
          </span>
          <GradeChip grade={grade?.grade} label={grade?.gradeLabel} />
          {item.status === "NeedsReview" &&
            (!grade?.grade || grade?.grade === "Ungraded") && (
              <span className="badge bg-gray-light text-gray-dark h-auto border-none text-xs font-semibold">
                {WAITING_FOR_REVIEW_TEXT}
              </span>
            )}
        </div>
        <div className="flex flex-col gap-1">
          <h2
            ref={headingRef}
            tabIndex={-1}
            className="font-nunito text-xl leading-snug font-black break-words text-black focus:outline-none md:text-2xl"
          >
            {item.opportunityTitle}
          </h2>
          <p className="text-gray-dark text-sm break-words">
            {item.organizationName}
          </p>
        </div>
        <nav
          aria-label="Open this course"
          className="flex flex-wrap gap-x-4 gap-y-1 text-sm font-semibold"
        >
          <Link
            href={`/organisations/${item.organizationId}/opportunities/${item.opportunityId}/info?returnUrl=${encodeURIComponent(`${QUEUE_PATH}/review`)}`}
            className="text-theme hover:underline"
          >
            Course page
          </Link>
          <Link
            href={learnerHref}
            target="_blank"
            rel="noopener noreferrer"
            className="text-theme inline-flex items-center gap-1 hover:underline"
          >
            See as learner
            <IoOpenOutline aria-hidden="true" className="h-3.5 w-3.5" />
            <span className="sr-only">(opens in a new tab)</span>
          </Link>
          <Link
            href={partnerHref}
            target="_blank"
            rel="noopener noreferrer"
            className="text-theme inline-flex items-center gap-1 hover:underline"
          >
            See as partner
            <IoOpenOutline aria-hidden="true" className="h-3.5 w-3.5" />
            <span className="sr-only">(opens in a new tab)</span>
          </Link>
        </nav>
      </header>

      {/* left to right: why it's here, the grade, and what holds it back with what learners said */}
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="flex min-w-0 flex-col gap-6">
          {/* WHY IT'S HERE */}
          <Section title="Why it's here">
            <div className="bg-yellow-tint flex flex-col gap-3 rounded-lg p-3">
              {reasons.length > 0 ? (
                <ul className="flex flex-col gap-1.5">
                  {reasons.map((reason) => (
                    <li
                      key={reason}
                      className="flex items-start gap-2 text-sm break-words text-black"
                    >
                      <IoEllipseOutline
                        aria-hidden="true"
                        className="text-yellow mt-1 h-3 w-3 shrink-0"
                      />
                      <span className="min-w-0">{reason}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-black">
                  The checks gave no reason. It is here because its confidence
                  is below the threshold.
                </p>
              )}
              <ConfidenceMeter confidence={item.confidence} />
            </div>
            {!item.aiUsed && (
              <p className="text-gray-dark text-xs">
                {item.aiModel
                  ? "Page reader: no AI ran. The course page was read with fixed rules."
                  : "Rules only: no course page could be read, so confidence is capped."}
              </p>
            )}
          </Section>

          {/* CHECKS */}
          <Section
            title="Checks"
            aside={
              <span className="text-gray-dark text-xs">
                Score {Math.round(item.score)} · Rubric {item.rubricVersion}
              </span>
            }
          >
            <ul className="divide-gray-light flex flex-col divide-y">
              {item.criteria.map((result) => {
                const outcome = checkResultOf(result);
                const { Icon, className } = CHECK_RESULT_STYLES[outcome];
                return (
                  <li
                    key={result.criterion}
                    className="flex flex-col gap-1 py-2.5 first:pt-0 last:pb-0"
                  >
                    <div className="flex flex-wrap items-center gap-2 text-sm font-semibold">
                      <Icon
                        aria-hidden="true"
                        className={`h-4 w-4 shrink-0 ${className}`}
                      />
                      {CRITERION_LABELS[result.criterion] ?? result.criterion}
                      <span className="text-gray-dark text-xs font-normal">
                        {outcome} · {Math.round(result.score)}
                      </span>
                      {result.hardGateFailed && (
                        <span className="badge bg-orange-light text-orange border-none text-xs font-semibold">
                          Hard gate failed
                        </span>
                      )}
                    </div>
                    <p className="text-gray-dark text-sm break-words">
                      {result.reason}
                    </p>
                    {result.recommendation && (
                      <p className="text-gray-dark text-xs break-words">
                        <span className="font-semibold">Suggested fix:</span>{" "}
                        {result.recommendation}
                      </p>
                    )}
                  </li>
                );
              })}
            </ul>
            {item.aiSummary && (
              <div className="bg-gray-light rounded-lg px-3 py-2">
                <h4 className="text-xs font-semibold">Evidence summary</h4>
                <p className="text-gray-dark text-sm break-words whitespace-pre-line">
                  {item.aiSummary}
                </p>
              </div>
            )}
            <p className="text-gray-dark text-xs">
              Assessed{" "}
              <Moment format={DATE_FORMAT_HUMAN} utc={true}>
                {item.dateAssessed}
              </Moment>
            </p>
          </Section>
        </div>

        <div className="flex min-w-0 flex-col gap-6">
          {/* GRADE */}
          <Section title="Grade preview">
            <GradeBreakdown
              grade={grade}
              waitingForReview={item.status === "NeedsReview"}
              showCaps={false}
            />
          </Section>
        </div>

        <div className="flex min-w-0 flex-col gap-6">
          {/* WHY IT HAS NO GRADE, or what held it down */}
          <GradeCaps grade={grade} standalone={true} />

          {/* LEARNER FEEDBACK: the search carries it; otherwise fetch it */}
          {item.feedback ? (
            <CourseFeedbackSummaryPanel
              summary={item.feedback}
              showComments={true}
            />
          ) : (
            <CourseFeedbackLazySummary
              opportunityId={item.opportunityId}
              showComments={true}
            />
          )}
        </div>
      </div>
    </article>
  );
};

const ShortcutHelp: React.FC<{ onClose: () => void }> = ({ onClose }) => (
  <div
    role="dialog"
    aria-label="Keyboard shortcuts"
    className="border-gray absolute right-0 bottom-full z-40 mb-2 w-64 rounded-lg border bg-white p-3 text-sm shadow-lg"
  >
    <div className="mb-2 flex items-center justify-between">
      <span className="font-semibold">Keyboard shortcuts</span>
      <button
        type="button"
        className="text-gray-dark text-xs hover:underline"
        onClick={onClose}
      >
        Close
      </button>
    </div>
    <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1">
      {[
        ["V", "Verify"],
        ["N", "Not verified"],
        ["S", "Skip"],
        ["?", "Show or hide this list"],
      ].map(([key, label]) => (
        <div key={key} className="contents">
          <dt>
            <kbd className="kbd kbd-xs">{key}</kbd>
          </dt>
          <dd className="text-gray-dark">{label}</dd>
        </div>
      ))}
    </dl>
  </div>
);

const DoneState: React.FC<{
  items: CourseQualityAssessment[];
  outcomes: Record<string, Outcome>;
  /** Pending courses not in this batch and not skipped this session. */
  remaining: number;
  /** Skipped this session in earlier batches, still waiting. */
  skippedEarlier: number;
  onRevisitSkipped: () => void;
  onNextBatch: () => void;
  onReviewSkipped: () => void;
}> = ({
  items,
  outcomes,
  remaining,
  skippedEarlier,
  onRevisitSkipped,
  onNextBatch,
  onReviewSkipped,
}) => {
  const counts = items.reduce(
    (acc, item) => {
      const outcome = outcomes[item.id];
      if (outcome) acc[outcome]++;
      else acc.Unseen++;
      return acc;
    },
    { Verified: 0, NotVerified: 0, Skipped: 0, Unseen: 0 },
  );
  const decided = items.filter((item) => outcomes[item.id]);

  return (
    <section className="motion-safe:animate-fade-in flex flex-col gap-5 rounded-2xl bg-white p-4 shadow-sm md:p-6">
      <div className="flex flex-col gap-1">
        <h2 className="font-nunito text-xl font-black text-black md:text-2xl">
          {items.length > 0
            ? "You reached the end of this batch"
            : skippedEarlier > 0
              ? "No new courses are left"
              : "Nothing is waiting for review"}
        </h2>
        <p className="text-gray-dark text-sm">
          {items.length === 0 && skippedEarlier === 0
            ? "Every assessment the checks were unsure about has a decision."
            : [
                remaining > 0
                  ? `${remaining} new ${remaining === 1 ? "course is" : "courses are"} still waiting after this batch.`
                  : items.length > 0
                    ? "No new courses are waiting after this batch."
                    : "",
                skippedEarlier > 0
                  ? `${skippedEarlier} you skipped earlier this session ${skippedEarlier === 1 ? "is" : "are"} still waiting.`
                  : "",
              ]
                .filter(Boolean)
                .join(" ")}
        </p>
      </div>

      {items.length > 0 && (
        <dl className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {(
            [
              ["Verified", counts.Verified],
              ["Not verified", counts.NotVerified],
              ["Skipped", counts.Skipped],
              ["Not opened", counts.Unseen],
            ] as const
          ).map(([label, value]) => (
            <div
              key={label}
              className="bg-gray-light flex flex-col rounded-lg px-3 py-2"
            >
              <dt className="text-gray-dark text-xs">{label}</dt>
              <dd className="font-nunito text-xl font-black">{value}</dd>
            </div>
          ))}
        </dl>
      )}

      {decided.length > 0 && (
        <ul className="divide-gray-light flex max-h-80 flex-col divide-y overflow-y-auto">
          {decided.map((item) => (
            <li
              key={item.id}
              className="flex items-start justify-between gap-3 py-2 text-sm"
            >
              <span className="min-w-0 break-words">
                {item.opportunityTitle}
                <span className="text-gray-dark block text-xs">
                  {item.organizationName}
                </span>
              </span>
              <span
                className={`shrink-0 text-xs font-semibold ${
                  outcomes[item.id] === "Skipped"
                    ? "text-gray-dark"
                    : "text-black"
                }`}
              >
                {OUTCOME_LABELS[outcomes[item.id]!]}
              </span>
            </li>
          ))}
        </ul>
      )}

      <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
        <Link href={QUEUE_PATH} className={BTN_SECONDARY}>
          Back to the queue
        </Link>
        {counts.Skipped + counts.Unseen > 0 && (
          <button
            type="button"
            className={BTN_SECONDARY}
            onClick={onRevisitSkipped}
          >
            Go back to skipped
          </button>
        )}
        {remaining === 0 && skippedEarlier > 0 && (
          <button
            type="button"
            className={BTN_SECONDARY}
            onClick={onReviewSkipped}
          >
            Review the {skippedEarlier} skipped
          </button>
        )}
        {remaining > 0 && (
          <button type="button" className={BTN_PRIMARY} onClick={onNextBatch}>
            Review the next batch
          </button>
        )}
      </div>
    </section>
  );
};

// ⚠️ SSR
export async function getServerSideProps(context: GetServerSidePropsContext) {
  const session = await getServerSession(context.req, context.res, authOptions);
  if (!session) return { props: { error: 401 } };
  if (!session.user?.roles?.includes(ROLE_ADMIN))
    return { props: { error: 403 } };
  return { props: { error: null } };
}

const CourseQualityReviewMode: NextPageWithLayout<{ error?: number }> = ({
  error,
}) => {
  const router = useRouter();
  const queryClient = useQueryClient();
  const organizationId = router.query.organisation?.toString() || null;

  const [batch, setBatch] = useState(0);
  const [index, setIndex] = useState(0);
  const [direction, setDirection] = useState<"forward" | "back">("forward");
  const [outcomes, setOutcomes] = useState<Record<string, Outcome>>({});
  const [decision, setDecision] = useState<PendingDecision | null>(null);
  const [showHelp, setShowHelp] = useState(false);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const movedByUser = useRef(false);
  // skipped in earlier batches of this session: left out of the next batch, kept per id
  const [skippedEarlier, setSkippedEarlier] = useState<string[]>([]);
  const excludedRef = useRef<ReadonlySet<string>>(new Set());

  const {
    data: results,
    error: searchError,
    isError,
    isLoading,
    refetch,
  } = useQuery({
    queryKey: [...SESSION_QUERY_KEY, organizationId, batch],
    queryFn: () =>
      fetchReviewBatch(
        (pageNumber, pageSize) =>
          searchCourseQualityReviews({
            reviewStatus: "Pending",
            organizationId,
            pageNumber,
            pageSize,
          }),
        excludedRef.current,
        BATCH_SIZE,
      ),
    enabled: !error && router.isReady,
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });

  const items = useMemo(() => results?.items ?? [], [results]);
  const current = index < items.length ? items[index]! : null;
  const decidedCount = items.filter((item) =>
    isDecided(outcomes[item.id]),
  ).length;
  const remaining = Math.max(0, (results?.totalCount ?? 0) - decidedCount);
  const remainingAfterBatch = results?.newAfterBatch ?? 0;
  const skippedThisBatch = items.filter(
    (item) => outcomes[item.id] === "Skipped",
  ).length;
  const skippedInSession = skippedEarlier.length + skippedThisBatch;

  const startBatch = useCallback((exclude: string[]) => {
    excludedRef.current = new Set(exclude);
    setSkippedEarlier(exclude);
    movedByUser.current = false;
    setOutcomes({});
    setIndex(0);
    setDirection("forward");
    setBatch((value) => value + 1);
  }, []);

  // move focus to the new card's title, so keyboard and screen reader users follow along
  useEffect(() => {
    if (!movedByUser.current) return;
    headingRef.current?.focus({ preventScroll: true });
    window.scrollTo({ top: 0, behavior: "auto" });
  }, [index]);

  const goTo = useCallback(
    (next: number) => {
      movedByUser.current = true;
      setDirection(next < index ? "back" : "forward");
      setIndex(Math.max(0, Math.min(items.length, next)));
    },
    [index, items.length],
  );

  /** The next course after `from` without a decision, or the end. */
  const nextOpen = useCallback(
    (from: number, decided: Record<string, Outcome>) => {
      for (let i = from + 1; i < items.length; i++)
        if (!isDecided(decided[items[i]!.id])) return i;
      return items.length;
    },
    [items],
  );

  const skip = useCallback(() => {
    if (!current) return;
    const next = { ...outcomes };
    if (!isDecided(next[current.id])) next[current.id] = "Skipped";
    setOutcomes(next);
    goTo(nextOpen(index, next));
  }, [current, outcomes, goTo, nextOpen, index]);

  const decisionMutation = useMutation({
    mutationFn: ({
      item,
      target,
      input,
    }: {
      item: CourseQualityAssessment;
      target: FinalStatus;
      input: ReviewDecisionInput;
    }) =>
      submitCourseQualityReview(item.id, {
        status: target,
        format: input.format,
        note: input.note,
      }),
    onSuccess: async (_, { item, target }) => {
      const next = { ...outcomes, [item.id]: target };
      setOutcomes(next);
      setDecision(null);
      toast.success(`Decision saved: ${STATUS_LABELS[target]}`, {
        toastId: "courseQualityDecision",
      });
      const at = items.findIndex((candidate) => candidate.id === item.id);
      goTo(nextOpen(at, next));
      // the queue, coverage, grades and partner notes can all change
      await queryClient.invalidateQueries({ queryKey: ["courseQuality"] });
    },
    onError: (mutationError) => {
      toast(<ApiErrors error={mutationError} />, {
        type: "error",
        toastId: "courseQualityDecisionError",
        autoClose: false,
        icon: false,
      });
    },
  });

  // one click decides and the next course slides in; the dialog only opens when Verified still
  // needs a format, which the API requires
  const { mutate: submitDecision, isPending: isDeciding } = decisionMutation;
  const decide = useCallback(
    (target: FinalStatus) => {
      if (!current || isDeciding || isDecided(outcomes[current.id])) return;
      setShowHelp(false);
      if (target === "Verified" && current.learnerCard.format === "Unknown")
        setDecision({ item: current, target });
      else
        submitDecision({
          item: current,
          target,
          input: { format: null, note: null },
        });
    },
    [current, outcomes, isDeciding, submitDecision],
  );

  // KEYBOARD: V, N, S, ?
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.defaultPrevented || e.metaKey || e.ctrlKey || e.altKey) return;
      if (decision) return;
      const target = e.target as HTMLElement | null;
      if (
        target?.closest(
          "input, textarea, select, [contenteditable='true'], [role='dialog'] button",
        )
      )
        return;
      switch (e.key) {
        case "v":
        case "V":
          decide("Verified");
          break;
        case "n":
        case "N":
          decide("NotVerified");
          break;
        case "s":
        case "S":
          skip();
          break;
        case "?":
          setShowHelp((shown) => !shown);
          break;
        case "Escape":
          setShowHelp(false);
          return;
        default:
          return;
      }
      e.preventDefault();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [decision, decide, skip]);

  const searchErrorStatus = axios.isAxiosError(searchError)
    ? searchError.response?.status
    : null;
  const resolvedError = error ?? searchErrorStatus;
  if (resolvedError === 401) return <Unauthenticated />;
  if (resolvedError === 403) return <Unauthorized />;
  if (error) return <InternalServerError />;

  const currentOutcome = current ? outcomes[current.id] : undefined;
  const progress =
    items.length > 0 ? Math.round((decidedCount / items.length) * 100) : 0;

  return (
    <>
      <Head>
        <title>Yoma | Review courses</title>
      </Head>

      <ReviewDecisionDialog
        decision={decision}
        isPending={decisionMutation.isPending}
        onClose={() => setDecision(null)}
        onSubmit={(input) => {
          if (decision)
            decisionMutation.mutate({
              item: decision.item,
              target: decision.target,
              input,
            });
        }}
      />

      <div className="container mx-auto flex w-full max-w-7xl flex-col gap-4 px-4 pt-24 pb-6">
        {/* PROGRESS */}
        <div className="flex flex-col gap-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <Link
              href={QUEUE_PATH}
              className="text-gray-dark inline-flex items-center gap-1 text-sm font-semibold hover:underline"
            >
              <IoArrowBack aria-hidden="true" className="h-4 w-4" />
              Queue
            </Link>
            <p className="text-sm" aria-live="polite">
              {current ? (
                <>
                  <span className="font-nunito font-black">{index + 1}</span> of{" "}
                  <span className="font-nunito font-black">{items.length}</span>
                  {remainingAfterBatch > 0 && (
                    <span className="text-gray-dark">
                      {" "}
                      · {remaining} waiting in all
                    </span>
                  )}
                  {skippedInSession > 0 && (
                    <span className="text-gray-dark">
                      {" "}
                      · {skippedInSession} skipped this session
                    </span>
                  )}
                  <span className="sr-only">: {current.opportunityTitle}</span>
                </>
              ) : items.length > 0 ? (
                "Batch done"
              ) : null}
            </p>
          </div>
          <div
            role="progressbar"
            aria-label="Decided in this batch"
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={progress}
            className="bg-gray h-1.5 w-full overflow-hidden rounded-full"
          >
            <div
              className="bg-purple h-full rounded-full transition-[width] duration-300 motion-reduce:transition-none"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>

        {/* CARD */}
        {isError ? (
          <div
            role="alert"
            className="flex flex-col items-center gap-3 rounded-2xl bg-white p-6 text-center"
          >
            <ApiErrors error={searchError} />
            <button
              type="button"
              className={BTN_SECONDARY}
              onClick={() => void refetch()}
            >
              Try again
            </button>
          </div>
        ) : isLoading || !router.isReady ? (
          <div className="flex h-64 items-center justify-center rounded-2xl bg-white">
            <span className="loading loading-spinner text-gray-dark" />
            <span className="sr-only">Loading courses to review</span>
          </div>
        ) : current ? (
          <div className="overflow-x-clip">
            <div
              key={current.id}
              className={`motion-safe:[animation-duration:320ms] ${
                direction === "forward"
                  ? "motion-safe:animate-slide-in-right"
                  : "motion-safe:animate-slide-in-left"
              }`}
            >
              <ReviewCard item={current} headingRef={headingRef} />
            </div>
          </div>
        ) : (
          <DoneState
            items={items}
            outcomes={outcomes}
            remaining={remainingAfterBatch}
            skippedEarlier={skippedEarlier.length}
            onRevisitSkipped={() => {
              const first = items.findIndex(
                (item) => !isDecided(outcomes[item.id]),
              );
              if (first >= 0) goTo(first);
            }}
            onNextBatch={() =>
              // anything in this batch without a decision counts as skipped
              startBatch([
                ...skippedEarlier,
                ...items
                  .filter((item) => !isDecided(outcomes[item.id]))
                  .map((item) => item.id),
              ])
            }
            onReviewSkipped={() => startBatch([])}
          />
        )}

        {/* ACTIONS */}
        {current && (
          <div
            className={`sticky ${STICKY_ACTION_BAR} z-30 -mx-1 flex flex-col gap-2 rounded-2xl bg-white/95 p-2 shadow-lg backdrop-blur sm:flex-row sm:items-center sm:justify-between`}
          >
            <div className="flex items-center justify-between gap-2 sm:justify-start">
              <div className="relative">
                <button
                  type="button"
                  className="text-gray-dark hidden text-xs hover:underline md:inline"
                  aria-expanded={showHelp}
                  onClick={() => setShowHelp((shown) => !shown)}
                >
                  Shortcuts <kbd className="kbd kbd-xs">?</kbd>
                </button>
                {showHelp && (
                  <ShortcutHelp onClose={() => setShowHelp(false)} />
                )}
              </div>
            </div>
            <div className="grid grid-cols-3 gap-2 sm:flex">
              <button
                type="button"
                className={`${BTN_DANGER} !min-w-0 !px-3 sm:!px-6`}
                disabled={
                  decisionMutation.isPending || isDecided(currentOutcome)
                }
                onClick={() => decide("NotVerified")}
              >
                Not verified
                <Kbd>N</Kbd>
              </button>
              <button
                type="button"
                className={`${BTN_SECONDARY} !min-w-0 !px-3 sm:!px-6`}
                disabled={decisionMutation.isPending}
                onClick={skip}
              >
                Skip
                <Kbd>S</Kbd>
              </button>
              <button
                type="button"
                className={`${BTN_PRIMARY} !min-w-0 !px-3 sm:!px-6`}
                disabled={
                  decisionMutation.isPending || isDecided(currentOutcome)
                }
                onClick={() => decide("Verified")}
              >
                Verify
                <Kbd>V</Kbd>
              </button>
            </div>
          </div>
        )}
      </div>
    </>
  );
};

CourseQualityReviewMode.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

CourseQualityReviewMode.theme = function getTheme() {
  return THEME_BLUE;
};

export default CourseQualityReviewMode;
