import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import { type GetServerSidePropsContext } from "next";
import { getServerSession } from "next-auth";
import Head from "next/head";
import Link from "next/link";
import { type ParsedUrlQuery } from "querystring";
import { useMemo, type ReactElement } from "react";
import Moment from "react-moment";
import type {
  CourseQualityCriterion,
  CourseQualityCriterionResult,
  CourseQualityStatus,
  PartnerFixNote,
} from "~/api/models/courseQuality";
import { getCourseQualityFixNotes } from "~/api/services/courseQualityAdmin";
import { getOrganisationById } from "~/api/services/organisations";
import { BTN_SECONDARY } from "~/components/Common/buttonStyles";
import {
  ListPageBody,
  ListPageHeader,
  ListPageShell,
} from "~/components/Common/ListPage/ListPageHeader";
import { ListPageResults } from "~/components/Common/ListPage/ListPageResults";
import MainLayout from "~/components/Layout/Main";
import NoRowsMessage from "~/components/NoRowsMessage";
import { ApiErrors } from "~/components/Status/ApiErrors";
import { Unauthenticated } from "~/components/Status/Unauthenticated";
import { Unauthorized } from "~/components/Status/Unauthorized";
import { DATE_FORMAT_HUMAN } from "~/lib/constants";
import { getThemeFromRole } from "~/lib/utils";
import { type NextPageWithLayout } from "~/pages/_app";
import { authOptions } from "~/server/auth";

interface IParams extends ParsedUrlQuery {
  id: string;
}

const CRITERION_LABELS: Record<CourseQualityCriterion, string> = {
  ListingLive: "Listing status",
  LinkWorks: "Course link",
  DatesAccurate: "Course dates",
  DataComplete: "Course details",
  FormatIdentified: "Course format",
};

const STATUS_LABELS: Record<CourseQualityStatus, string> = {
  Verified: "Verified",
  NeedsReview: "Being reviewed",
  NotVerified: "Not yet verified",
};

const hasHardGateIssue = (note: PartnerFixNote) =>
  note.issues.some((issue) => issue.hardGateFailed);

// highest priority first: the issues that keep a course from being verified at any score
const sortIssues = (issues: CourseQualityCriterionResult[]) =>
  [...issues].sort(
    (a, b) => Number(b.hardGateFailed) - Number(a.hardGateFailed),
  );

const FixNoteCard: React.FC<{
  note: PartnerFixNote;
  organisationId: string;
}> = ({ note, organisationId }) => {
  const issues = sortIssues(note.issues);
  const steps = issues.filter((issue) => issue.recommendation);

  return (
    <article className="flex flex-col gap-4 rounded-lg bg-white p-4 shadow-sm md:p-6">
      <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
        <Link
          href={`/organisations/${organisationId}/opportunities/${note.opportunityId}`}
          className="text-theme text-base font-semibold break-words hover:underline"
        >
          {note.opportunityTitle}
        </Link>
        <div className="flex shrink-0 flex-wrap gap-2">
          {hasHardGateIssue(note) && (
            <span className="badge bg-orange-light text-orange border-none text-xs font-semibold">
              Fix first
            </span>
          )}
          <span className="badge bg-gray-light text-gray-dark border-none text-xs font-semibold">
            {STATUS_LABELS[note.status]}
          </span>
        </div>
      </div>

      {/* WHY */}
      <div className="flex flex-col gap-1">
        <h4 className="text-sm font-semibold">Why it is held back</h4>
        <p className="text-gray-dark text-sm break-words whitespace-pre-line">
          {note.summary}
        </p>
        {issues.length > 0 && (
          <ul className="mt-1 flex flex-col gap-1">
            {issues.map((issue) => (
              <li
                key={issue.criterion}
                className="text-gray-dark text-sm break-words"
              >
                <span className="font-semibold">
                  {CRITERION_LABELS[issue.criterion] ?? issue.criterion}:
                </span>{" "}
                {issue.reason}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* STEPS */}
      {steps.length > 0 && (
        <div className="flex flex-col gap-1">
          <h4 className="text-sm font-semibold">
            Steps to improve its visibility
          </h4>
          <ol className="text-gray-dark ml-5 flex list-decimal flex-col gap-1 text-sm">
            {steps.map((issue) => (
              <li key={issue.criterion} className="break-words">
                {issue.recommendation}
              </li>
            ))}
          </ol>
        </div>
      )}

      <div className="text-gray-dark text-xs">
        Last checked{" "}
        <Moment format={DATE_FORMAT_HUMAN} utc={true}>
          {note.dateAssessed}
        </Moment>
      </div>
    </article>
  );
};

// ⚠️ SSR
export async function getServerSideProps(context: GetServerSidePropsContext) {
  const { id } = context.params as IParams;
  const session = await getServerSession(context.req, context.res, authOptions);

  // 👇 ensure authenticated
  if (!session) {
    return {
      props: {
        error: 401,
      },
    };
  }

  // 👇 set theme based on role
  const theme = getThemeFromRole(session, id);

  // NB: access to the organisation is enforced by the API; a 403 renders Unauthorized below
  return {
    props: {
      id: id,
      theme: theme,
      error: null,
    },
  };
}

const CourseQualityFixNotes: NextPageWithLayout<{
  id: string;
  theme: string;
  error?: number;
}> = ({ id, error }) => {
  const { data: organisation } = useQuery({
    queryKey: ["organisation", id],
    queryFn: () => getOrganisationById(id),
    enabled: !error,
  });

  const {
    data: notes,
    error: notesError,
    isError,
    isLoading,
    refetch,
  } = useQuery({
    queryKey: ["courseQuality", "fixNotes", id],
    queryFn: () => getCourseQualityFixNotes(id),
    enabled: !error,
  });

  const sortedNotes = useMemo(
    () =>
      [...(notes ?? [])].sort(
        (a, b) =>
          Number(hasHardGateIssue(b)) - Number(hasHardGateIssue(a)) ||
          a.score - b.score,
      ),
    [notes],
  );

  const resolvedError =
    error ??
    (axios.isAxiosError(notesError) ? notesError.response?.status : null);

  if (resolvedError === 401) return <Unauthenticated />;
  if (resolvedError === 403) return <Unauthorized />;

  return (
    <>
      <Head>
        <title>Yoma | Course Quality</title>
      </Head>

      <ListPageShell>
        <ListPageHeader
          title="Course Quality"
          description={
            organisation?.name
              ? `Ways to help ${organisation.name}'s courses reach more learners.`
              : "Ways to help your courses reach more learners."
          }
        />

        <ListPageBody>
          <p className="text-gray-dark text-sm">
            These notes are shown here in Yoma only. They are not emailed, so
            check back here to see what to improve. A note updates after the
            course has been reassessed. Self-paced courses are verified as
            self-paced and show &ldquo;Start anytime&rdquo; instead of dates.
          </p>

          {isError ? (
            <div
              role="alert"
              className="flex flex-col items-center gap-3 rounded-lg bg-white p-6 text-center"
            >
              <ApiErrors error={notesError} />
              <button
                type="button"
                className={BTN_SECONDARY}
                onClick={() => void refetch()}
              >
                Try again
              </button>
            </div>
          ) : (
            <ListPageResults isLoading={isLoading}>
              {sortedNotes.length > 0 ? (
                <div className="flex flex-col gap-4">
                  {sortedNotes.map((note) => (
                    <FixNoteCard
                      key={note.assessmentId}
                      note={note}
                      organisationId={id}
                    />
                  ))}
                </div>
              ) : (
                <NoRowsMessage
                  title="No notes right now"
                  description="None of your courses need changes at the moment."
                />
              )}
            </ListPageResults>
          )}
        </ListPageBody>
      </ListPageShell>
    </>
  );
};

CourseQualityFixNotes.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

// 👇 return theme from component properties. this is set server-side (getServerSideProps)
CourseQualityFixNotes.theme = function getTheme(
  page: ReactElement<{ theme: string }>,
) {
  return page.props.theme;
};

export default CourseQualityFixNotes;
