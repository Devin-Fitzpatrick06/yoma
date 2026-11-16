import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { type GetServerSidePropsContext } from "next";
import { getServerSession } from "next-auth";
import Head from "next/head";
import Link from "next/link";
import { useRouter } from "next/router";
import {
  useDeferredValue,
  useEffect,
  useMemo,
  useState,
  type ReactElement,
  type ReactNode,
} from "react";
import {
  IoArrowBack,
  IoArrowDown,
  IoArrowUp,
  IoWarningOutline,
} from "react-icons/io5";
import Moment from "react-moment";
import { toast } from "react-toastify";
import type {
  CourseGrade,
  CourseGradeFactor,
  CourseGradeFactorRow,
  CourseGradeRubric,
  CourseGradeRubricState,
} from "~/api/models/courseQuality";
import {
  getCourseGradeFactors,
  getCourseGradeRubric,
  resetCourseGradeRubric,
  runCourseGrading,
  saveCourseGradeRubric,
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
import { STATUS_LABELS } from "~/components/CourseQuality/reviewLabels";
import MainLayout from "~/components/Layout/Main";
import { ApiErrors } from "~/components/Status/ApiErrors";
import { InternalServerError } from "~/components/Status/InternalServerError";
import { Unauthenticated } from "~/components/Status/Unauthenticated";
import { Unauthorized } from "~/components/Status/Unauthorized";
import { useConfirmationModalContext } from "~/context/modalConfirmationContext";
import { DATE_FORMAT_HUMAN, ROLE_ADMIN, THEME_BLUE } from "~/lib/constants";
import {
  bandShares,
  GRADE_FACTOR_LABELS,
  GRADE_FACTORS,
  gradeDistribution,
  gradeLabelOf,
  GRADES,
  isBetterGrade,
  normaliseWeights,
  regradeCatalogue,
  type RegradedCourse,
  validateRubric,
  withBandShare,
} from "~/lib/courseGrading";
import { type NextPageWithLayout } from "~/pages/_app";
import { authOptions } from "~/server/auth";

// ─────────────────────────────────────────────────────────────────────────────
// The editable grading rubric. Every edit regrades the whole catalogue in the browser from the
// stored factor scores (lib/courseGrading.ts), so the distribution and the courses that would
// change show at once. Saving stores an admin override and regrades on the server.
// ─────────────────────────────────────────────────────────────────────────────

const RUBRIC_QUERY_KEY = ["courseQuality", "grading", "rubric"];
const FACTORS_QUERY_KEY = ["courseQuality", "grading", "factors"];
const WEIGHT_MAX = 50;
const CHANGES_PAGE = 50;

type SortKey = "title" | "organisation" | "grade" | "score";

const clone = (rubric: CourseGradeRubric): CourseGradeRubric =>
  JSON.parse(JSON.stringify(rubric)) as CourseGradeRubric;

const clampInt = (value: string, min: number, max: number): number => {
  const parsed = Math.round(Number(value));
  return Number.isFinite(parsed) ? Math.max(min, Math.min(max, parsed)) : min;
};

const Panel: React.FC<{
  title: string;
  description?: string;
  children: ReactNode;
}> = ({ title, description, children }) => (
  <section className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm md:p-5">
    <div className="flex flex-col gap-0.5">
      <h2 className="font-nunito text-lg font-black text-black">{title}</h2>
      {description && <p className="text-gray-dark text-sm">{description}</p>}
    </div>
    {children}
  </section>
);

const NumberField: React.FC<{
  label: string;
  value: number;
  min: number;
  max: number;
  onChange: (value: number) => void;
  suffix?: string;
  className?: string;
  hideLabel?: boolean;
}> = ({
  label,
  value,
  min,
  max,
  onChange,
  suffix,
  className = "",
  hideLabel,
}) => (
  <label className={`flex flex-col gap-1 text-sm ${className}`}>
    <span className={hideLabel ? "sr-only" : "font-semibold"}>{label}</span>
    <span className="flex items-center gap-1">
      <input
        type="number"
        inputMode="numeric"
        className="input input-sm w-20"
        min={min}
        max={max}
        value={value}
        onChange={(e) => onChange(clampInt(e.target.value, min, max))}
      />
      {suffix && <span className="text-gray-dark text-xs">{suffix}</span>}
    </span>
  </label>
);

const DistributionChart: React.FC<{
  current: Partial<Record<CourseGrade, number>>;
  preview: Record<CourseGrade, number>;
  labels: (grade: CourseGrade) => string;
}> = ({ current, preview, labels }) => {
  const gradedNow = GRADES.filter((g) => g !== "Ungraded").reduce(
    (sum, g) => sum + (current[g] ?? 0),
    0,
  );
  const gradedPreview = GRADES.filter((g) => g !== "Ungraded").reduce(
    (sum, g) => sum + preview[g],
    0,
  );
  const share = (count: number, total: number) =>
    total > 0 ? Math.round((count / total) * 1000) / 10 : 0;
  const max = Math.max(
    1,
    ...GRADES.map((g) => Math.max(current[g] ?? 0, preview[g])),
  );

  return (
    <div className="flex flex-col gap-3">
      <div className="text-gray-dark flex flex-wrap gap-4 text-xs">
        <span className="inline-flex items-center gap-1.5">
          <span className="bg-gray-dark h-2.5 w-2.5 rounded-sm" /> Now
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="bg-purple h-2.5 w-2.5 rounded-sm" /> With your
          changes
        </span>
      </div>
      <table className="w-full text-sm">
        <caption className="sr-only">
          Courses per grade, now and with your changes
        </caption>
        <thead className="sr-only">
          <tr>
            <th>Grade</th>
            <th>Now and with your changes</th>
          </tr>
        </thead>
        <tbody>
          {GRADES.map((grade) => {
            const now = current[grade] ?? 0;
            const next = preview[grade];
            const graded = grade !== "Ungraded";
            return (
              <tr key={grade} className="align-top">
                <th
                  scope="row"
                  className="w-24 py-1.5 pr-3 text-left font-normal sm:w-36"
                >
                  <span className="font-nunito font-black">
                    {graded ? grade : "–"}
                  </span>{" "}
                  <span className="text-gray-dark text-xs">
                    {labels(grade)}
                  </span>
                </th>
                <td className="py-1.5">
                  <div className="flex flex-col gap-1">
                    <div className="flex items-center gap-2">
                      <div className="bg-gray-light h-2 flex-1 overflow-hidden rounded-full">
                        <div
                          className="bg-gray-dark h-full rounded-full"
                          style={{ width: `${(now / max) * 100}%` }}
                        />
                      </div>
                      <span className="text-gray-dark w-28 shrink-0 text-right text-xs">
                        {now}
                        {graded && ` · ${share(now, gradedNow)}%`}
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <div className="bg-gray-light h-2 flex-1 overflow-hidden rounded-full">
                        <div
                          className="bg-purple h-full rounded-full transition-[width] duration-200 motion-reduce:transition-none"
                          style={{ width: `${(next / max) * 100}%` }}
                        />
                      </div>
                      <span className="w-28 shrink-0 text-right text-xs font-semibold">
                        {next}
                        {graded && ` · ${share(next, gradedPreview)}%`}
                      </span>
                    </div>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <p className="text-gray-dark text-xs">
        Shares are of graded courses. Not verified courses and courses with too
        little data are not graded.
      </p>
    </div>
  );
};

const GradeChange: React.FC<{ from: CourseGrade; to: CourseGrade }> = ({
  from,
  to,
}) => {
  const up = isBetterGrade(to, from);
  return (
    <span className="inline-flex items-center gap-1 whitespace-nowrap">
      <span className="font-nunito font-black">
        {from === "Ungraded" ? "–" : from}
      </span>
      <span aria-hidden="true" className="text-gray-dark">
        →
      </span>
      <span className="sr-only">to</span>
      <span className="font-nunito font-black">
        {to === "Ungraded" ? "–" : to}
      </span>
      {up ? (
        <IoArrowUp aria-label="up" className="text-purple h-3.5 w-3.5" />
      ) : (
        <IoArrowDown aria-label="down" className="text-orange h-3.5 w-3.5" />
      )}
    </span>
  );
};

const ChangesTable: React.FC<{
  rows: CourseGradeFactorRow[];
  results: Map<string, RegradedCourse>;
}> = ({ rows, results }) => {
  const [sort, setSort] = useState<{ key: SortKey; asc: boolean }>({
    key: "grade",
    asc: true,
  });
  const [limit, setLimit] = useState(CHANGES_PAGE);

  const changes = useMemo(() => {
    const changed = rows
      .map((row) => ({ row, next: results.get(row.opportunityId)! }))
      .filter(({ row, next }) => next && next.grade !== row.grade);
    const gradeOrder = (g: CourseGrade) => GRADES.indexOf(g);
    const compare = {
      title: (a: (typeof changed)[0], b: (typeof changed)[0]) =>
        a.row.opportunityTitle.localeCompare(b.row.opportunityTitle),
      organisation: (a: (typeof changed)[0], b: (typeof changed)[0]) =>
        a.row.organizationName.localeCompare(b.row.organizationName),
      // biggest moves first: up, then down
      grade: (a: (typeof changed)[0], b: (typeof changed)[0]) =>
        gradeOrder(a.next.grade) -
        gradeOrder(a.row.grade) -
        (gradeOrder(b.next.grade) - gradeOrder(b.row.grade)),
      score: (a: (typeof changed)[0], b: (typeof changed)[0]) =>
        b.next.score - a.next.score,
    }[sort.key];
    return changed.sort((a, b) => (sort.asc ? compare(a, b) : -compare(a, b)));
  }, [rows, results, sort]);

  const header = (key: SortKey, label: string, className = "") => (
    <th
      scope="col"
      className={`py-2 pr-3 text-left text-xs font-semibold ${className}`}
      aria-sort={
        sort.key === key ? (sort.asc ? "ascending" : "descending") : "none"
      }
    >
      <button
        type="button"
        className="inline-flex items-center gap-1 hover:underline"
        onClick={() =>
          setSort((s) => ({ key, asc: s.key === key ? !s.asc : true }))
        }
      >
        {label}
        {sort.key === key &&
          (sort.asc ? (
            <IoArrowUp aria-hidden="true" className="h-3 w-3" />
          ) : (
            <IoArrowDown aria-hidden="true" className="h-3 w-3" />
          ))}
      </button>
    </th>
  );

  if (changes.length === 0)
    return (
      <p className="text-gray-dark text-sm">
        No course would change grade with these settings.
      </p>
    );

  return (
    <div className="flex flex-col gap-2">
      <p className="text-sm">
        <span className="font-nunito font-black">{changes.length}</span>{" "}
        {changes.length === 1 ? "course" : "courses"} would change grade.
      </p>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[560px] text-sm">
          <thead className="border-gray border-b">
            <tr>
              {header("title", "Course")}
              {header("organisation", "Organisation")}
              {header("score", "Score")}
              {header("grade", "Grade")}
            </tr>
          </thead>
          <tbody className="divide-gray-light divide-y">
            {changes.slice(0, limit).map(({ row, next }) => (
              <tr key={row.opportunityId} className="align-top">
                <td className="py-2 pr-3">
                  <Link
                    href={`/opportunities/${row.opportunityId}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="break-words hover:underline"
                  >
                    {row.opportunityTitle}
                  </Link>
                  <span className="text-gray-dark block text-xs">
                    {STATUS_LABELS[row.status]}
                    {next.caps.length > 0 && ` · ${next.caps[0]}`}
                  </span>
                </td>
                <td className="text-gray-dark py-2 pr-3 break-words">
                  {row.organizationName}
                </td>
                <td className="py-2 pr-3 whitespace-nowrap">
                  {row.score} →{" "}
                  <span className="font-semibold">{next.score}</span>
                </td>
                <td className="py-2 pr-3">
                  <GradeChange from={row.grade} to={next.grade} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {changes.length > limit && (
        <button
          type="button"
          className={`${BTN_SECONDARY} self-center`}
          onClick={() => setLimit((value) => value + CHANGES_PAGE)}
        >
          Show {Math.min(CHANGES_PAGE, changes.length - limit)} more
        </button>
      )}
    </div>
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

const CourseGradeRubricPage: NextPageWithLayout<{ error?: number }> = ({
  error,
}) => {
  const router = useRouter();
  const queryClient = useQueryClient();
  const modalContext = useConfirmationModalContext();
  const [draft, setDraft] = useState<CourseGradeRubric | null>(null);

  const {
    data: state,
    error: rubricError,
    isError: isRubricError,
    refetch: refetchRubric,
  } = useQuery({
    queryKey: RUBRIC_QUERY_KEY,
    queryFn: getCourseGradeRubric,
    enabled: !error && router.isReady,
  });

  const {
    data: rows,
    error: factorsError,
    isError: isFactorsError,
    refetch: refetchFactors,
  } = useQuery({
    queryKey: FACTORS_QUERY_KEY,
    queryFn: getCourseGradeFactors,
    enabled: !error && router.isReady,
    staleTime: 5 * 60 * 1000,
  });

  // start (and restart after a save) from the active rubric
  useEffect(() => {
    if (state) setDraft(clone(state.rubric));
  }, [state]);

  const deferredDraft = useDeferredValue(draft);
  const preview = useMemo(
    () =>
      deferredDraft && rows ? regradeCatalogue(rows, deferredDraft) : null,
    [rows, deferredDraft],
  );
  const baseline = useMemo(
    () => (state && rows ? regradeCatalogue(rows, state.rubric) : null),
    [rows, state],
  );
  // rows whose stored grade the browser cannot reproduce from the saved rubric
  const drift = useMemo(
    () =>
      rows && baseline
        ? rows.filter(
            (row) => baseline.get(row.opportunityId)?.grade !== row.grade,
          ).length
        : 0,
    [rows, baseline],
  );

  const isDirty =
    !!draft &&
    !!state &&
    JSON.stringify(draft) !== JSON.stringify(state.rubric);
  const problems = draft ? validateRubric(draft) : [];

  const onSettled = (next: CourseGradeRubricState, message: string) => {
    queryClient.setQueryData(RUBRIC_QUERY_KEY, next);
    void queryClient.invalidateQueries({ queryKey: ["courseQuality"] });
    toast.success(message, { toastId: "courseGradeRubric" });
  };
  const onError = (mutationError: unknown) =>
    toast(<ApiErrors error={mutationError as Error} />, {
      type: "error",
      toastId: "courseGradeRubricError",
      autoClose: false,
      icon: false,
    });

  const saveMutation = useMutation({
    mutationFn: saveCourseGradeRubric,
    onSuccess: (next) => onSettled(next, "Rubric saved and courses regraded"),
    onError,
  });
  const resetMutation = useMutation({
    mutationFn: resetCourseGradeRubric,
    onSuccess: (next) =>
      onSettled(next, "Rubric reset to default and courses regraded"),
    onError,
  });
  const runMutation = useMutation({
    mutationFn: runCourseGrading,
    onSuccess: (next) => onSettled(next, "Courses regraded"),
    onError,
  });
  const isBusy =
    saveMutation.isPending || resetMutation.isPending || runMutation.isPending;

  const resolvedError =
    error ??
    [rubricError, factorsError]
      .map((e) => (axios.isAxiosError(e) ? e.response?.status : null))
      .find((status) => status === 401 || status === 403);
  if (resolvedError === 401) return <Unauthenticated />;
  if (resolvedError === 403) return <Unauthorized />;
  if (error) return <InternalServerError />;

  const update = (change: (rubric: CourseGradeRubric) => void) =>
    setDraft((current) => {
      if (!current) return current;
      const next = clone(current);
      change(next);
      return next;
    });

  const onSave = async () => {
    if (!draft || !preview) return;
    const dist = gradeDistribution(preview.values());
    const ok = await modalContext.showConfirmation(
      "Save the rubric?",
      <p className="text-gray-dark mt-2 text-sm">
        This replaces the {state?.isDefault ? "default" : "saved"} rubric and
        regrades every course now. Expected: {dist.A} A, {dist.B} B, {dist.C} C,{" "}
        {dist.D} D. Learners see the new grades straight away.
      </p>,
    );
    if (ok) saveMutation.mutate(draft);
  };

  const onReset = async () => {
    const ok = await modalContext.showConfirmation(
      "Reset to the default rubric?",
      <p className="text-gray-dark mt-2 text-sm">
        This removes the saved override, returns to the configured defaults and
        regrades every course now.
      </p>,
    );
    if (ok) resetMutation.mutate();
  };

  const weights = draft ? normaliseWeights(draft) : null;
  const shares = draft ? bandShares(draft.bands) : [];
  const labelOf = (grade: CourseGrade) =>
    gradeLabelOf(grade, draft?.bands ?? state?.rubric.bands ?? []);

  return (
    <>
      <Head>
        <title>Yoma | Grading rubric</title>
      </Head>

      <ListPageShell>
        <ListPageHeader
          title="Grading rubric"
          description="How courses are graded, separately from Verified. Change a weight or a band and see the whole catalogue regrade before you save."
        />

        <ListPageBody>
          <Link
            href="/admin/course-quality"
            className="text-gray-dark inline-flex items-center gap-1 self-start text-sm font-semibold hover:underline"
          >
            <IoArrowBack aria-hidden="true" className="h-4 w-4" />
            Course Quality
          </Link>

          {isRubricError || isFactorsError ? (
            <div
              role="alert"
              className="flex flex-col items-center gap-3 rounded-lg bg-white p-6 text-center"
            >
              <ApiErrors error={(rubricError ?? factorsError) as Error} />
              <button
                type="button"
                className={BTN_SECONDARY}
                onClick={() => {
                  void refetchRubric();
                  void refetchFactors();
                }}
              >
                Try again
              </button>
            </div>
          ) : !state || !draft || !weights ? (
            <div className="flex h-48 items-center justify-center rounded-lg bg-white">
              <span className="loading loading-spinner text-gray-dark" />
              <span className="sr-only">Loading the rubric</span>
            </div>
          ) : (
            <>
              {/* ACTIVE RUBRIC */}
              <section className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm md:flex-row md:items-center md:justify-between md:p-5">
                <div className="flex min-w-0 flex-col gap-1 text-sm">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="badge bg-gray-light text-gray-dark border-none text-xs font-semibold">
                      {state.isDefault ? "Default" : "Saved override"}
                    </span>
                    <span className="text-gray-dark text-xs">
                      Version {state.rubric.version}
                    </span>
                    {isDirty && (
                      <span className="badge bg-yellow-tint text-yellow border-none text-xs font-semibold">
                        Unsaved changes
                      </span>
                    )}
                  </div>
                  <p className="text-gray-dark">
                    {state.isDefault ? (
                      "From configuration. Nobody has saved an override."
                    ) : (
                      <>
                        Saved by {state.updatedByEmail ?? "unknown"}
                        {state.dateUpdated && (
                          <>
                            {" "}
                            on{" "}
                            <Moment format={DATE_FORMAT_HUMAN} utc={true}>
                              {state.dateUpdated}
                            </Moment>
                          </>
                        )}
                        .
                      </>
                    )}
                  </p>
                </div>
                <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
                  <button
                    type="button"
                    className={BTN_SECONDARY}
                    disabled={isBusy}
                    onClick={() => runMutation.mutate()}
                  >
                    {runMutation.isPending ? "Grading..." : "Run grading now"}
                  </button>
                  <button
                    type="button"
                    className={BTN_DANGER}
                    disabled={isBusy || state.isDefault}
                    onClick={() => void onReset()}
                  >
                    {resetMutation.isPending
                      ? "Resetting..."
                      : "Reset to default"}
                  </button>
                  <button
                    type="button"
                    className={BTN_SECONDARY}
                    disabled={isBusy || !isDirty}
                    onClick={() => setDraft(clone(state.rubric))}
                  >
                    Discard changes
                  </button>
                  <button
                    type="button"
                    className={BTN_PRIMARY}
                    disabled={
                      isBusy || !isDirty || problems.length > 0 || !rows
                    }
                    onClick={() => void onSave()}
                  >
                    {saveMutation.isPending ? "Saving..." : "Save"}
                  </button>
                </div>
              </section>

              {problems.length > 0 && (
                <div
                  role="alert"
                  className="bg-orange-light flex items-start gap-2 rounded-lg px-3 py-2 text-sm"
                >
                  <IoWarningOutline
                    aria-hidden="true"
                    className="text-orange mt-0.5 h-4 w-4 shrink-0"
                  />
                  <div className="flex flex-col gap-1">
                    <p className="font-semibold">
                      Save is off until{" "}
                      {problems.length === 1
                        ? "this is fixed"
                        : "these are fixed"}
                      :
                    </p>
                    <ul className="ml-4 list-disc">
                      {problems.map((problem) => (
                        <li key={problem}>{problem}</li>
                      ))}
                    </ul>
                  </div>
                </div>
              )}

              <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
                <div className="flex min-w-0 flex-col gap-4">
                  {/* WEIGHTS */}
                  <Panel
                    title="Factor weights"
                    description="Each factor is scored 0 to 100. The weights are shared out to 100%."
                  >
                    <ul className="flex flex-col gap-3">
                      {GRADE_FACTORS.map((factor: CourseGradeFactor) => {
                        const weight = draft.factors[factor]?.weight ?? 0;
                        const id = `weight-${factor}`;
                        return (
                          <li
                            key={factor}
                            className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1"
                          >
                            <label
                              htmlFor={id}
                              className="text-sm font-semibold"
                            >
                              {GRADE_FACTOR_LABELS[factor]}
                            </label>
                            <span className="text-gray-dark text-right text-xs">
                              <span className="font-semibold text-black">
                                {Math.round(weights[factor] * 10) / 10}%
                              </span>{" "}
                              of the score
                            </span>
                            <input
                              id={id}
                              type="range"
                              className="range range-xs range-primary w-full"
                              min={0}
                              max={Math.max(WEIGHT_MAX, weight)}
                              step={1}
                              value={weight}
                              onChange={(e) =>
                                update((r) => {
                                  r.factors[factor].weight = Number(
                                    e.target.value,
                                  );
                                })
                              }
                            />
                            <NumberField
                              label={`${GRADE_FACTOR_LABELS[factor]} weight`}
                              hideLabel
                              value={weight}
                              min={0}
                              max={100}
                              onChange={(value) =>
                                update((r) => {
                                  r.factors[factor].weight = value;
                                })
                              }
                            />
                          </li>
                        );
                      })}
                    </ul>
                  </Panel>

                  {/* BANDS */}
                  <Panel
                    title="Grade bands"
                    description="Shares of graded courses, best first. The last band takes the rest. A band also needs its minimum score."
                  >
                    <div className="overflow-x-auto">
                      <table className="w-full min-w-[320px] text-sm">
                        <thead>
                          <tr className="text-gray-dark text-left text-xs">
                            <th scope="col" className="pr-2 pb-1 font-semibold">
                              Grade
                            </th>
                            <th scope="col" className="pr-2 pb-1 font-semibold">
                              Label
                            </th>
                            <th scope="col" className="pr-2 pb-1 font-semibold">
                              Share
                            </th>
                            <th scope="col" className="pb-1 font-semibold">
                              Min score
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {draft.bands.map((band, index) => {
                            const last = index === draft.bands.length - 1;
                            return (
                              <tr key={band.grade}>
                                <th
                                  scope="row"
                                  className="font-nunito py-1 pr-2 text-left font-black"
                                >
                                  {band.grade}
                                </th>
                                <td className="py-1 pr-2">
                                  <input
                                    type="text"
                                    aria-label={`${band.grade} label`}
                                    className="input input-sm w-full min-w-24"
                                    maxLength={40}
                                    value={band.label}
                                    onChange={(e) =>
                                      update((r) => {
                                        r.bands[index]!.label = e.target.value;
                                      })
                                    }
                                  />
                                </td>
                                <td className="py-1 pr-2">
                                  {last ? (
                                    <span className="text-gray-dark text-xs whitespace-nowrap">
                                      The rest ({shares[index]}%)
                                    </span>
                                  ) : (
                                    <NumberField
                                      label={`${band.grade} share`}
                                      hideLabel
                                      value={shares[index] ?? 0}
                                      min={0}
                                      max={100}
                                      suffix="%"
                                      onChange={(value) =>
                                        update((r) => {
                                          r.bands = withBandShare(
                                            r.bands,
                                            index,
                                            value,
                                          );
                                        })
                                      }
                                    />
                                  )}
                                </td>
                                <td className="py-1">
                                  <NumberField
                                    label={`${band.grade} minimum score`}
                                    hideLabel
                                    value={band.minScore}
                                    min={0}
                                    max={100}
                                    onChange={(value) =>
                                      update((r) => {
                                        r.bands[index]!.minScore = value;
                                      })
                                    }
                                  />
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  </Panel>

                  {/* RULES */}
                  <Panel
                    title="Rules"
                    description="Floors an A must also meet, or it drops to B, and how courses waiting for review are graded."
                  >
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <NumberField
                        label="A: syllabus at least"
                        value={draft.aMinSyllabusScore}
                        min={0}
                        max={100}
                        onChange={(value) =>
                          update((r) => {
                            r.aMinSyllabusScore = value;
                          })
                        }
                      />
                      <NumberField
                        label="A: feedback at least"
                        value={draft.aMinFeedbackScore}
                        min={0}
                        max={100}
                        onChange={(value) =>
                          update((r) => {
                            r.aMinFeedbackScore = value;
                          })
                        }
                      />
                      <NumberField
                        label="Feedback floor from"
                        suffix="answers"
                        value={draft.aFeedbackFloorMinResponses}
                        min={0}
                        max={1000}
                        onChange={(value) =>
                          update((r) => {
                            r.aFeedbackFloorMinResponses = value;
                          })
                        }
                      />
                      <NumberField
                        label="Grade with data on at least"
                        suffix="factors"
                        value={draft.minFactorsWithData}
                        min={0}
                        max={GRADE_FACTORS.length}
                        onChange={(value) =>
                          update((r) => {
                            r.minFactorsWithData = value;
                          })
                        }
                      />
                      <label className="flex flex-col gap-1 text-sm sm:col-span-2">
                        <span className="font-semibold">
                          Courses waiting for review
                        </span>
                        <select
                          className="select select-sm w-full sm:w-64"
                          value={draft.needsReviewMaxGrade}
                          onChange={(e) =>
                            update((r) => {
                              r.needsReviewMaxGrade = e.target
                                .value as CourseGrade;
                            })
                          }
                        >
                          <option value="Ungraded">
                            Not graded until reviewed
                          </option>
                          {draft.bands.map((band) => (
                            <option key={band.grade} value={band.grade}>
                              At most {band.grade} · {band.label}
                            </option>
                          ))}
                        </select>
                        <span className="text-gray-dark text-xs">
                          {draft.needsReviewMaxGrade === "Ungraded"
                            ? "They get no grade and are left out of the shares until a reviewer confirms them."
                            : `They are graded with the rest but reach ${draft.needsReviewMaxGrade} at most.`}
                        </span>
                      </label>
                    </div>
                  </Panel>
                </div>

                <div className="flex min-w-0 flex-col gap-4">
                  {/* DISTRIBUTION */}
                  <Panel
                    title="Grade distribution"
                    description={
                      rows
                        ? `${rows.length.toLocaleString("en-GB")} courses, regraded in your browser as you edit.`
                        : "Loading the courses..."
                    }
                  >
                    {preview ? (
                      <DistributionChart
                        current={state.distribution}
                        preview={gradeDistribution(preview.values())}
                        labels={labelOf}
                      />
                    ) : (
                      <span className="loading loading-dots text-gray-dark" />
                    )}
                  </Panel>

                  {/* CHANGES */}
                  <Panel
                    title="Courses that would change"
                    description="Compared with each course's stored grade."
                  >
                    {drift > 0 && (
                      <p className="bg-gray-light text-gray-dark rounded-lg px-3 py-2 text-xs">
                        {drift}{" "}
                        {drift === 1 ? "course differs" : "courses differ"} from
                        its stored grade even before your changes, for example
                        from feedback answer counts the browser cannot see, or a
                        grading run that has not happened yet.
                      </p>
                    )}
                    {rows && preview ? (
                      <ChangesTable rows={rows} results={preview} />
                    ) : (
                      <span className="loading loading-dots text-gray-dark" />
                    )}
                  </Panel>
                </div>
              </div>
            </>
          )}
        </ListPageBody>
      </ListPageShell>
    </>
  );
};

CourseGradeRubricPage.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

CourseGradeRubricPage.theme = function getTheme() {
  return THEME_BLUE;
};

export default CourseGradeRubricPage;
