import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { GetServerSidePropsContext } from "next";
import { getServerSession } from "next-auth";
import { useMemo, useState, type ReactElement } from "react";
import type { PathwayProfileRequest } from "~/api/models/pathway";
import {
  getMyYoma,
  getPathwayQuestions,
  updatePathwayProfile,
} from "~/api/services/pathway";
import { BTN_SECONDARY } from "~/components/Common/buttonStyles";
import { ListPageSkeleton } from "~/components/Common/ListPage/ListPageResults";
import MainLayout from "~/components/Layout/Main";
import MyYomaView from "~/components/MyYoma/MyYomaView";
import { MyYomaShell, MyYomaSignedOut } from "~/components/MyYoma/MyYomaShell";
import PathwayOnboarding from "~/components/MyYoma/PathwayOnboarding";
import { ApiErrors } from "~/components/Status/ApiErrors";
import { answersFromProfile, emptyAnswers } from "~/lib/pathway";
import type { NextPageWithLayout } from "~/pages/_app";
import { authOptions } from "~/server/auth";

// ─────────────────────────────────────────────────────────────────────────────
// My Yoma (/my-yoma). Signed out: what it is and a sign-in button. Signed in without a profile
// (the API answers 404): the onboarding questions. Otherwise the youth's path. "Change my answers"
// reopens the onboarding with the saved answers.
// ─────────────────────────────────────────────────────────────────────────────

// ⚠️ SSR
export async function getServerSideProps(context: GetServerSidePropsContext) {
  const session = await getServerSession(context.req, context.res, authOptions);
  return { props: { signedIn: !!session } };
}

const PATHWAY_KEY = ["pathway"] as const;

const LoadError: React.FC<{ error: unknown; onRetry: () => void }> = ({
  error,
  onRetry,
}) => (
  <div
    role="alert"
    className="mx-auto flex w-full max-w-2xl flex-col items-center gap-3 rounded-lg bg-white p-6 text-center"
  >
    <ApiErrors error={error} />
    <button type="button" className={BTN_SECONDARY} onClick={onRetry}>
      Try again
    </button>
  </div>
);

const MyYomaSignedIn: React.FC = () => {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);

  const myYomaQuery = useQuery({
    queryKey: [...PATHWAY_KEY, "me"],
    queryFn: getMyYoma,
  });
  const myYoma = myYomaQuery.data;
  const onboarding = myYoma === null || editing;

  const questionsQuery = useQuery({
    queryKey: [...PATHWAY_KEY, "questions"],
    queryFn: getPathwayQuestions,
    staleTime: Infinity,
    enabled: onboarding,
  });
  const questions = questionsQuery.data?.questions;

  const saveMutation = useMutation({
    mutationFn: (request: PathwayProfileRequest) =>
      updatePathwayProfile(request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: PATHWAY_KEY });
      setEditing(false);
      window.scrollTo({ top: 0 });
    },
  });

  const initialAnswers = useMemo(
    () =>
      editing && myYoma && questions
        ? answersFromProfile(myYoma.profile, questions)
        : emptyAnswers(),
    [editing, myYoma, questions],
  );

  if (myYomaQuery.isLoading)
    return (
      <MyYomaShell stage="path">
        <ListPageSkeleton />
      </MyYomaShell>
    );

  if (myYomaQuery.isError)
    return (
      <MyYomaShell stage="path">
        <LoadError
          error={myYomaQuery.error}
          onRetry={() => void myYomaQuery.refetch()}
        />
      </MyYomaShell>
    );

  if (onboarding)
    return (
      <MyYomaShell stage="onboarding">
        {questionsQuery.isError ? (
          <LoadError
            error={questionsQuery.error}
            onRetry={() => void questionsQuery.refetch()}
          />
        ) : !questions ? (
          <ListPageSkeleton />
        ) : (
          <PathwayOnboarding
            key={editing ? "edit" : "new"}
            questions={questions}
            initialAnswers={initialAnswers}
            isSaving={saveMutation.isPending}
            onSubmit={(request) => saveMutation.mutate(request)}
            onCancel={editing ? () => setEditing(false) : undefined}
            error={
              saveMutation.isError ? (
                <ApiErrors error={saveMutation.error} />
              ) : undefined
            }
          />
        )}
      </MyYomaShell>
    );

  return (
    <MyYomaShell stage="path">
      {myYoma && (
        <MyYomaView
          myYoma={myYoma}
          onChangeAnswers={() => {
            saveMutation.reset();
            setEditing(true);
            window.scrollTo({ top: 0 });
          }}
        />
      )}
    </MyYomaShell>
  );
};

const MyYomaPage: NextPageWithLayout<{ signedIn: boolean }> = ({ signedIn }) =>
  signedIn ? (
    <MyYomaSignedIn />
  ) : (
    <MyYomaShell stage="signedOut">
      <MyYomaSignedOut />
    </MyYomaShell>
  );

MyYomaPage.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

export default MyYomaPage;
