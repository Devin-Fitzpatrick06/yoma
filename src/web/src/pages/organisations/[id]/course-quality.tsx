import { useQuery } from "@tanstack/react-query";
import axios from "axios";
import { type GetServerSidePropsContext } from "next";
import { getServerSession } from "next-auth";
import Head from "next/head";
import { type ParsedUrlQuery } from "querystring";
import { useMemo, type ReactElement } from "react";
import { getCourseQualityFixNotes } from "~/api/services/courseQualityAdmin";
import { getOrganisationById } from "~/api/services/organisations";
import { BTN_SECONDARY } from "~/components/Common/buttonStyles";
import {
  ListPageBody,
  ListPageHeader,
  ListPageShell,
} from "~/components/Common/ListPage/ListPageHeader";
import { ListPageResults } from "~/components/Common/ListPage/ListPageResults";
import CourseFeedbackLazySummary from "~/components/CourseQuality/CourseFeedbackLazySummary";
import FixNoteCard, {
  hasHardGateIssue,
} from "~/components/CourseQuality/FixNoteCard";
import MainLayout from "~/components/Layout/Main";
import NoRowsMessage from "~/components/NoRowsMessage";
import { ApiErrors } from "~/components/Status/ApiErrors";
import { Unauthenticated } from "~/components/Status/Unauthenticated";
import { Unauthorized } from "~/components/Status/Unauthorized";
import { getThemeFromRole } from "~/lib/utils";
import { type NextPageWithLayout } from "~/pages/_app";
import { authOptions } from "~/server/auth";

interface IParams extends ParsedUrlQuery {
  id: string;
}

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
                      titleHref={`/organisations/${id}/opportunities/${note.opportunityId}`}
                      learnerHref={`/opportunities/${note.opportunityId}`}
                    >
                      {/* what learners liked and disliked; never their comments */}
                      <CourseFeedbackLazySummary
                        opportunityId={note.opportunityId}
                        showComments={false}
                      />
                    </FixNoteCard>
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
