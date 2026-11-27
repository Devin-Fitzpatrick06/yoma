import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import axios from "axios";
import { type GetServerSidePropsContext } from "next";
import { getServerSession } from "next-auth";
import Head from "next/head";
import Link from "next/link";
import { useRouter } from "next/router";
import { useCallback, useEffect, useMemo, useState } from "react";
import type { ReactElement } from "react";
import { IoMdArrowForward } from "react-icons/io";
import Moment from "react-moment";
import { toast } from "react-toastify";
import type {
  CourseGrade,
  CourseQualityAssessment,
  CourseQualityReviewSearchFilter,
  CourseQualityReviewStatus,
} from "~/api/models/courseQuality";
import { getPathwayHealth } from "~/api/services/pathway";
import {
  getCourseQualityCoverage,
  searchCourseQualityReviews,
  submitCourseQualityReview,
} from "~/api/services/courseQualityAdmin";
import {
  BTN_DANGER,
  BTN_PRIMARY,
  BTN_SECONDARY,
} from "~/components/Common/buttonStyles";
import {
  ListPageBody,
  ListPageHeader,
  ListPageShell,
} from "~/components/Common/ListPage/ListPageHeader";
import {
  ListPagePagination,
  ListPageResults,
} from "~/components/Common/ListPage/ListPageResults";
import ListPageTabs from "~/components/Common/ListPage/ListPageTabs";
import { WAITING_FOR_REVIEW_TEXT } from "~/components/CourseQuality/GradeBreakdown";
import GradeChip from "~/components/CourseQuality/GradeChip";
import PathwayHealthPanel from "~/components/CourseQuality/PathwayHealthPanel";
import ReviewDecisionDialog, {
  type PendingDecision,
  type ReviewDecisionInput,
} from "~/components/CourseQuality/ReviewDecisionDialog";
import {
  CRITERION_LABELS,
  type FinalStatus,
  FORMAT_LABELS,
  getDecisionLabel,
  STATUS_BADGES,
  STATUS_LABELS,
} from "~/components/CourseQuality/reviewLabels";
import MainLayout from "~/components/Layout/Main";
import NoRowsMessage from "~/components/NoRowsMessage";
import { ApiErrors } from "~/components/Status/ApiErrors";
import { InternalServerError } from "~/components/Status/InternalServerError";
import { Unauthenticated } from "~/components/Status/Unauthenticated";
import { Unauthorized } from "~/components/Status/Unauthorized";
import { env } from "~/env.mjs";
import { useAdminOpportunityOrganisationsQuery } from "~/hooks/useOpportunityMutations";
import {
  DATE_FORMAT_HUMAN,
  PAGE_SIZE,
  ROLE_ADMIN,
  THEME_BLUE,
} from "~/lib/constants";
import { type NextPageWithLayout } from "~/pages/_app";
import { authOptions } from "~/server/auth";

const BASE_PATH = "/admin/course-quality";
const ALL_STATUSES = "All";
const COURSE_QUALITY_QUERY_KEY = ["courseQuality"];
const REVIEW_QUERY_KEY = [...COURSE_QUALITY_QUERY_KEY, "review"];
const COVERAGE_QUERY_KEY = [...COURSE_QUALITY_QUERY_KEY, "coverage"];
const PENDING_COUNT_QUERY_KEY = [...REVIEW_QUERY_KEY, "pendingCount"];
const PATHWAY_HEALTH_QUERY_KEY = ["pathway", "health"];
const ALL_GRADES = "";

type ReviewTab = CourseQualityReviewStatus | typeof ALL_STATUSES;

const REVIEW_TABS: { value: ReviewTab; label: string }[] = [
  { value: "Pending", label: "Pending" },
  { value: "Confirmed", label: "Confirmed" },
  { value: "Overridden", label: "Overridden" },
  { value: "NotRequired", label: "Not required" },
  { value: ALL_STATUSES, label: "All" },
];

const GRADE_FILTERS: {
  value: CourseGrade | typeof ALL_GRADES;
  label: string;
}[] = [
  { value: ALL_GRADES, label: "All grades" },
  { value: "A", label: "A" },
  { value: "B", label: "B" },
  { value: "C", label: "C" },
  { value: "D", label: "D" },
  { value: "Ungraded", label: "Not graded" },
];

const parseGrade = (value: unknown): CourseGrade | null =>
  (GRADE_FILTERS.find((grade) => grade.value && grade.value === value)
    ?.value as CourseGrade | undefined) ?? null;

const parseReviewTab = (value: unknown): ReviewTab =>
  REVIEW_TABS.find((tab) => tab.value === value)?.value ?? "Pending";

const Metric: React.FC<{ label: string; value: string }> = ({
  label,
  value,
}) => (
  <div className="bg-gray-light flex flex-col rounded-lg px-3 py-2">
    <span className="text-gray-dark text-xs">{label}</span>
    <span className="text-sm font-semibold">{value}</span>
  </div>
);

const CoverageSummary: React.FC<{ enabled: boolean }> = ({ enabled }) => {
  const {
    data: coverage,
    isError,
    isLoading,
    refetch,
  } = useQuery({
    queryKey: COVERAGE_QUERY_KEY,
    queryFn: getCourseQualityCoverage,
    enabled,
  });

  return (
    <section
      aria-label="Coverage"
      className="flex flex-col gap-2 rounded-lg bg-white p-4 shadow-sm"
    >
      {isError && !coverage ? (
        <div role="alert" className="flex items-center gap-3 text-sm">
          Coverage could not be loaded.
          <button
            type="button"
            className={BTN_SECONDARY}
            onClick={() => void refetch()}
          >
            Try again
          </button>
        </div>
      ) : isLoading || !coverage ? (
        <p className="text-gray-dark text-sm">Loading coverage...</p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8">
            <Metric label="Total courses" value={`${coverage.totalCourses}`} />
            <Metric label="Assessed" value={`${coverage.assessed}`} />
            <Metric label="Not assessed" value={`${coverage.notAssessed}`} />
            <Metric
              label="Missing or suspect dates"
              value={`${coverage.missingOrSuspectDates}`}
            />
            <Metric label="Course" value={`${coverage.course}`} />
            <Metric label="Webinar" value={`${coverage.webinar}`} />
            <Metric label="Unknown" value={`${coverage.unknown}`} />
            <Metric label="Card ready" value={`${coverage.cardReady}`} />
          </div>
          <p className="text-gray-dark text-xs">
            Course, Webinar and Unknown add up to Assessed, not Total courses.
          </p>
        </>
      )}
    </section>
  );
};

// W5: steps tracked, events and drop-off ("Collecting from the pilot" until the API has enough)
const PathwayHealthSummary: React.FC<{ enabled: boolean }> = ({ enabled }) => {
  const { data, isError, isLoading, refetch } = useQuery({
    queryKey: PATHWAY_HEALTH_QUERY_KEY,
    queryFn: getPathwayHealth,
    enabled,
  });

  return (
    <PathwayHealthPanel
      health={data}
      isError={isError}
      isLoading={isLoading}
      onRetry={() => void refetch()}
    />
  );
};

const AssessmentCard: React.FC<{
  item: CourseQualityAssessment;
  returnUrl: string;
  disabled: boolean;
  onDecide: (item: CourseQualityAssessment, target: FinalStatus) => void;
}> = ({ item, returnUrl, disabled, onDecide }) => (
  <article className="flex flex-col gap-4 rounded-lg bg-white p-4 shadow-sm md:p-6">
    {/* TITLE */}
    <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
      <div className="flex min-w-0 flex-col gap-1">
        <Link
          href={`/organisations/${item.organizationId}/opportunities/${item.opportunityId}/info?returnUrl=${encodeURIComponent(returnUrl)}`}
          className="text-theme text-base font-semibold break-words hover:underline"
        >
          {item.opportunityTitle}
        </Link>
        <Link
          href={`/organisations/${item.organizationId}/course-quality`}
          className="text-gray-dark text-sm break-words hover:underline"
        >
          {item.organizationName}
        </Link>
      </div>
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        <GradeChip
          grade={item.grade?.grade ?? item.learnerCard?.grade}
          label={item.grade?.gradeLabel ?? item.learnerCard?.gradeLabel}
        />
        {item.status === "NeedsReview" &&
          (!(item.grade?.grade ?? item.learnerCard?.grade) ||
            (item.grade?.grade ?? item.learnerCard?.grade) === "Ungraded") && (
            <span className="badge bg-gray-light text-gray-dark h-auto border-none text-xs font-semibold">
              {WAITING_FOR_REVIEW_TEXT}
            </span>
          )}
        <span className="badge bg-gray-light text-gray-dark border-none text-xs font-semibold">
          {FORMAT_LABELS[item.learnerCard?.format] ?? FORMAT_LABELS.Unknown}
        </span>
        <span
          className={`badge shrink-0 border-none text-xs font-semibold ${STATUS_BADGES[item.status]}`}
        >
          {STATUS_LABELS[item.status]}
        </span>
      </div>
    </div>

    {/* SCORES */}
    <div className="grid grid-cols-2 gap-2 md:grid-cols-4">
      <Metric label="Score" value={`${Math.round(item.score)}`} />
      <Metric
        label="Confidence"
        value={`${Math.round(item.confidence * 100)}%`}
      />
      <Metric
        label="AI status"
        value={
          item.aiUsed
            ? (item.aiModel ?? "AI used")
            : item.aiModel
              ? "Page reader"
              : "Rules only"
        }
      />
      <Metric
        label="Review"
        value={
          REVIEW_TABS.find((tab) => tab.value === item.reviewStatus)?.label ??
          item.reviewStatus
        }
      />
    </div>

    {/* AI NOTE */}
    {!item.aiUsed && (
      <p
        role="note"
        className="bg-yellow-tint text-gray-dark rounded-lg px-3 py-2 text-sm"
      >
        {item.learnerCard.selfPaced
          ? 'Self-paced: the course page was read and matches this listing. It is verified as self-paced with "Start anytime", and its dates were not checked.'
          : item.aiModel
            ? "Page reader: no AI ran. The course page was read with fixed rules for its dates, cost and format, so check it yourself before deciding."
            : "Rules only: no course page could be read for this assessment, so its content was not compared and confidence is capped. Check it yourself before deciding."}
      </p>
    )}

    {/* AI SUMMARY */}
    {item.aiSummary && (
      <div className="flex flex-col gap-1">
        <h4 className="text-sm font-semibold">AI summary</h4>
        <p className="text-gray-dark text-sm break-words whitespace-pre-line">
          {item.aiSummary}
        </p>
      </div>
    )}

    {/* CRITERIA */}
    <div className="flex flex-col gap-2">
      <h4 className="text-sm font-semibold">Criteria</h4>
      <ul className="divide-gray-light flex flex-col divide-y">
        {item.criteria.map((result) => (
          <li
            key={result.criterion}
            className="flex flex-col gap-1 py-2 first:pt-0 last:pb-0"
          >
            <div className="flex flex-wrap items-center gap-2 text-sm font-medium">
              {CRITERION_LABELS[result.criterion] ?? result.criterion}
              <span className="text-gray-dark text-xs font-normal">
                Score {Math.round(result.score)}
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
          </li>
        ))}
      </ul>
    </div>

    <div className="text-gray-dark text-xs">
      Assessed{" "}
      <Moment format={DATE_FORMAT_HUMAN} utc={true}>
        {item.dateAssessed}
      </Moment>{" "}
      · Rubric {item.rubricVersion}
    </div>

    {/* DECISION */}
    {item.reviewStatus === "Pending" ? (
      <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
        <button
          type="button"
          className={BTN_DANGER}
          disabled={disabled}
          onClick={() => onDecide(item, "NotVerified")}
        >
          {getDecisionLabel(item.status, "NotVerified")}
        </button>
        <button
          type="button"
          className={BTN_PRIMARY}
          disabled={disabled}
          onClick={() => onDecide(item, "Verified")}
        >
          {getDecisionLabel(item.status, "Verified")}
        </button>
      </div>
    ) : (
      item.dateReviewed && (
        <div className="bg-gray-light flex flex-col gap-1 rounded-lg px-3 py-2 text-sm">
          <span>
            Reviewed by {item.reviewedByEmail ?? "unknown"} on{" "}
            <Moment format={DATE_FORMAT_HUMAN} utc={true}>
              {item.dateReviewed}
            </Moment>
          </span>
          {item.reviewNote && (
            <span className="text-gray-dark break-words">
              {item.reviewNote}
            </span>
          )}
        </div>
      )
    )}
  </article>
);

// ⚠️ SSR
export async function getServerSideProps(context: GetServerSidePropsContext) {
  // 👇 ensure authenticated and authorized
  const session = await getServerSession(context.req, context.res, authOptions);
  if (!session) {
    return {
      props: {
        error: 401,
      },
    };
  }
  if (!session.user?.roles?.includes(ROLE_ADMIN)) {
    return {
      props: {
        error: 403,
      },
    };
  }

  // NB: the filters are driven by the querystring (router.query), not by props
  return {
    props: {
      error: null,
      pathwayEnabled: env.NEXT_PUBLIC_PATHWAY_ENABLED === true,
    },
  };
}

const CourseQualityReview: NextPageWithLayout<{
  error?: number;
  pathwayEnabled?: boolean;
}> = ({ error, pathwayEnabled = false }) => {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [decision, setDecision] = useState<PendingDecision | null>(null);

  // 👇 filters are driven by the querystring
  const reviewTab = parseReviewTab(router.query.reviewStatus);
  const organizationId = router.query.organisation?.toString() || null;
  const grade = parseGrade(router.query.grade);
  const parsedPage = parseInt(router.query.page?.toString() ?? "", 10);
  const pageNumber = parsedPage > 0 ? parsedPage : 1;

  const filter = useMemo<CourseQualityReviewSearchFilter>(
    () => ({
      reviewStatus: reviewTab === ALL_STATUSES ? null : reviewTab,
      organizationId,
      grade,
      pageNumber,
      pageSize: PAGE_SIZE,
    }),
    [reviewTab, organizationId, grade, pageNumber],
  );

  // how many are waiting, for the "Start reviewing" call to action
  const { data: pendingCount } = useQuery({
    queryKey: PENDING_COUNT_QUERY_KEY,
    queryFn: async () =>
      (
        await searchCourseQualityReviews({
          reviewStatus: "Pending",
          organizationId: null,
          pageNumber: 1,
          pageSize: 1,
        })
      ).totalCount,
    enabled: !error,
  });

  const { data: organisations } = useAdminOpportunityOrganisationsQuery({
    enabled: !error,
  });

  const {
    data: results,
    error: searchError,
    isError,
    isLoading,
    isPlaceholderData: isShowingPreviousResults,
    refetch,
  } = useQuery({
    queryKey: [...REVIEW_QUERY_KEY, filter],
    queryFn: () => searchCourseQualityReviews(filter),
    enabled: !error && router.isReady,
    placeholderData: keepPreviousData,
  });

  const buildUrl = useCallback(
    (
      tab: ReviewTab,
      organisation: string | null,
      page: number,
      gradeFilter: CourseGrade | null,
    ) => {
      const params = new URLSearchParams();
      if (tab !== "Pending") params.set("reviewStatus", tab);
      if (organisation) params.set("organisation", organisation);
      if (gradeFilter) params.set("grade", gradeFilter);
      if (page > 1) params.set("page", page.toString());
      const query = params.toString();
      return query ? `${BASE_PATH}?${query}` : BASE_PATH;
    },
    [],
  );

  // a decision can empty the last page: step back to one that has rows
  const isPageEmpty =
    !!results &&
    !isShowingPreviousResults &&
    results.items.length === 0 &&
    pageNumber > 1;
  useEffect(() => {
    if (isPageEmpty)
      void router.replace(
        buildUrl(reviewTab, organizationId, pageNumber - 1, grade),
        undefined,
        { scroll: false },
      );
  }, [
    isPageEmpty,
    router,
    buildUrl,
    reviewTab,
    organizationId,
    pageNumber,
    grade,
  ]);

  const decisionMutation = useMutation({
    mutationFn: ({
      assessmentId,
      status,
      reviewFormat,
      reviewNote,
    }: {
      assessmentId: string;
      status: FinalStatus;
      reviewFormat: ReviewDecisionInput["format"];
      reviewNote: string | null;
    }) =>
      submitCourseQualityReview(assessmentId, {
        status,
        format: reviewFormat,
        note: reviewNote,
      }),
    onSuccess: async (_, { status }) => {
      // a decision can change the queue, the coverage counts and the partner notes
      await queryClient.invalidateQueries({
        queryKey: COURSE_QUALITY_QUERY_KEY,
      });
      setDecision(null);
      toast.success(`Decision saved: ${STATUS_LABELS[status]}`, {
        toastId: "courseQualityDecision",
      });
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

  const onDecide = useCallback(
    (item: CourseQualityAssessment, target: FinalStatus) =>
      setDecision({ item, target }),
    [],
  );

  const onSubmitDecision = useCallback(
    ({ format, note }: ReviewDecisionInput) => {
      if (!decision) return;
      decisionMutation.mutate({
        assessmentId: decision.item.id,
        status: decision.target,
        reviewFormat: format,
        reviewNote: note,
      });
    },
    [decision, decisionMutation],
  );

  const searchErrorStatus = axios.isAxiosError(searchError)
    ? searchError.response?.status
    : null;
  const resolvedError = error ?? searchErrorStatus;

  if (resolvedError === 401) return <Unauthenticated />;
  if (resolvedError === 403) return <Unauthorized />;
  if (error) return <InternalServerError />;

  return (
    <>
      <Head>
        <title>Yoma | Ziqi reviewer queue</title>
      </Head>

      {/* DECISION POPUP */}
      <ReviewDecisionDialog
        decision={decision}
        isPending={decisionMutation.isPending}
        onClose={() => setDecision(null)}
        onSubmit={onSubmitDecision}
      />

      <ListPageShell>
        <ListPageHeader
          title="Ziqi reviewer queue"
          description="Review course assessments the checks were not confident about, then confirm or override the result."
        >
          <ListPageTabs
            ariaLabel="Filter by review status"
            tabs={REVIEW_TABS.map((tab) => ({
              key: `course_quality_tab_${tab.value}`,
              label: tab.label,
              href: buildUrl(tab.value, organizationId, 1, grade),
              selected: reviewTab === tab.value,
            }))}
          />
        </ListPageHeader>

        <ListPageBody>
          {/* START REVIEWING: one course at a time */}
          <section
            aria-label="Reviewer mode"
            className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm sm:flex-row sm:items-center sm:justify-between md:p-5"
          >
            <div className="flex min-w-0 flex-col gap-0.5">
              <h2 className="font-nunito text-lg font-black text-black">
                {pendingCount == null
                  ? "Courses waiting for review"
                  : pendingCount === 1
                    ? "1 course is waiting for review"
                    : `${pendingCount} courses are waiting for review`}
              </h2>
              <p className="text-gray-dark text-sm">
                Go through them one at a time, with why each is here, its grade
                and what learners and partners see.
              </p>
            </div>
            <div className="flex shrink-0 flex-col gap-2 sm:flex-row sm:items-center">
              <Link
                href={`${BASE_PATH}/rubric`}
                className="text-gray-dark text-sm font-semibold hover:underline"
              >
                Grading rubric
              </Link>
              <Link
                href={`${BASE_PATH}/review`}
                aria-disabled={pendingCount === 0}
                className={`${BTN_PRIMARY} gap-2 ${
                  pendingCount === 0 ? "pointer-events-none opacity-60" : ""
                }`}
              >
                Start reviewing
                {pendingCount != null && pendingCount > 0 && (
                  <span className="rounded-full bg-white/25 px-2 text-xs font-bold">
                    {pendingCount}
                  </span>
                )}
                <IoMdArrowForward aria-hidden="true" className="h-4 w-4" />
              </Link>
            </div>
          </section>

          {/* COVERAGE */}
          <CoverageSummary enabled={!error} />

          {/* PATHWAY HEALTH (Ziqi pathways switch only) */}
          {pathwayEnabled && <PathwayHealthSummary enabled={!error} />}

          {/* FILTERS */}
          <div className="flex flex-col gap-3 md:flex-row">
            <label className="flex flex-col gap-1 text-sm md:max-w-sm md:flex-1">
              <span className="font-semibold">Organisation</span>
              <select
                className="select select-sm w-full"
                value={organizationId ?? ""}
                onChange={(e) =>
                  void router.push(
                    buildUrl(reviewTab, e.target.value || null, 1, grade),
                    undefined,
                    { scroll: false },
                  )
                }
              >
                <option value="">All organisations</option>
                {organisations?.map((organisation) => (
                  <option key={organisation.id} value={organisation.id}>
                    {organisation.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex flex-col gap-1 text-sm md:w-44">
              <span className="font-semibold">Grade</span>
              <select
                className="select select-sm w-full"
                value={grade ?? ALL_GRADES}
                onChange={(e) =>
                  void router.push(
                    buildUrl(
                      reviewTab,
                      organizationId,
                      1,
                      parseGrade(e.target.value),
                    ),
                    undefined,
                    { scroll: false },
                  )
                }
              >
                {GRADE_FILTERS.map((option) => (
                  <option key={option.value || "all"} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {/* RESULTS */}
          {isError ? (
            <div
              role="alert"
              className="flex flex-col items-center gap-3 rounded-lg bg-white p-6 text-center"
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
          ) : (
            <ListPageResults
              isLoading={isLoading || !router.isReady}
              isShowingPreviousResults={isShowingPreviousResults}
              id="results"
            >
              {results?.items.length ? (
                <div className="flex flex-col gap-4">
                  {results.items.map((item) => (
                    <AssessmentCard
                      key={item.id}
                      item={item}
                      returnUrl={router.asPath}
                      disabled={
                        decisionMutation.isPending || isShowingPreviousResults
                      }
                      onDecide={onDecide}
                    />
                  ))}

                  <ListPagePagination
                    currentPage={pageNumber}
                    totalItems={results.totalCount}
                    pageSize={PAGE_SIZE}
                    isShowingPreviousResults={isShowingPreviousResults}
                    onClick={(page) =>
                      void router.push(
                        buildUrl(reviewTab, organizationId, page, grade),
                        undefined,
                        { scroll: false },
                      )
                    }
                  />
                </div>
              ) : (
                <NoRowsMessage
                  title={
                    reviewTab === "Pending"
                      ? "No courses are waiting for review"
                      : "No assessments found"
                  }
                  description="Try another status or organisation."
                />
              )}
            </ListPageResults>
          )}
        </ListPageBody>
      </ListPageShell>
    </>
  );
};

CourseQualityReview.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

CourseQualityReview.theme = function getTheme() {
  return THEME_BLUE;
};

export default CourseQualityReview;
