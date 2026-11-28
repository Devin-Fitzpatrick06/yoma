import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAtomValue } from "jotai";
import type { GetServerSidePropsContext } from "next";
import { getServerSession } from "next-auth";
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactElement,
} from "react";
import type {
  MyYoma,
  PathwayProfileRequest,
  PathwayStepEventType,
} from "~/api/models/pathway";
import {
  getMyYoma,
  getPathwayQuestions,
  getPathwayStepAlternatives,
  recordPathwayStepEvent,
  setPathwayStepLocked,
  swapPathwayStep,
  updatePathwayProfile,
} from "~/api/services/pathway";
import { BTN_SECONDARY } from "~/components/Common/buttonStyles";
import { ListPageSkeleton } from "~/components/Common/ListPage/ListPageResults";
import MainLayout from "~/components/Layout/Main";
import { useJourneyListingSaved } from "~/components/Journey/useJourneyListingSaved";
import FirstVisitFlow from "~/components/MyYoma/FirstVisitFlow";
import { FIRST_VISIT_REDIRECT_KEY } from "~/components/MyYoma/firstVisit";
import MyYomaMapView from "~/components/MyYoma/MyYomaMapView";
import { MyYomaShell, MyYomaSignedOut } from "~/components/MyYoma/MyYomaShell";
import { ApiErrors } from "~/components/Status/ApiErrors";
import { env } from "~/env.mjs";
import { answersFromProfile, emptyAnswers } from "~/lib/pathway";
import { userProfileAtom } from "~/lib/store";
import type { NextPageWithLayout } from "~/pages/_app";
import { authOptions } from "~/server/auth";

// ─────────────────────────────────────────────────────────────────────────────
// My Yoma (/my-yoma), behind NEXT_PUBLIC_PATHWAY_ENABLED (a 404 when off). Signed out: what it is
// and a sign-in button. Signed in without a path (the API answers 404): the full-screen first
// visit (YoID card, the questions Yoma's preferences did not answer, "Building your path"), then
// the journey map assembling itself. "Change my answers" reopens the slides prefilled.
// ─────────────────────────────────────────────────────────────────────────────

// ⚠️ SSR
export async function getServerSideProps(context: GetServerSidePropsContext) {
  if (!env.NEXT_PUBLIC_PATHWAY_ENABLED) return { notFound: true };
  const session = await getServerSession(context.req, context.res, authOptions);
  return { props: { signedIn: !!session } };
}

const PATHWAY_KEY = ["pathway"] as const;
const MY_YOMA_KEY = [...PATHWAY_KEY, "me"] as const;

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
  const userProfile = useAtomValue(userProfileAtom);
  const [editing, setEditing] = useState(false);
  // true from "Build my path" until the map shows, so the flow stays up while the path loads
  const [building, setBuilding] = useState(false);
  const [animateEntrance, setAnimateEntrance] = useState(false);

  // the youth reached My Yoma: the home page does not need to send them here again
  useEffect(() => {
    try {
      window.sessionStorage.setItem(FIRST_VISIT_REDIRECT_KEY, "1");
    } catch {
      // storage blocked: the redirect then relies on the pathway profile alone
    }
  }, []);

  const myYomaQuery = useQuery({
    queryKey: MY_YOMA_KEY,
    queryFn: getMyYoma,
  });
  const myYoma = myYomaQuery.data;
  const inFlow = myYoma === null || editing || building;
  // connected journey (demo): the PathFinder card at the end of the route links to the listing
  const journeySaved = useJourneyListingSaved(!inFlow);

  const questionsQuery = useQuery({
    queryKey: [...PATHWAY_KEY, "questions"],
    queryFn: getPathwayQuestions,
    staleTime: Infinity,
    enabled: inFlow,
  });
  const questions = questionsQuery.data?.questions;
  const answeredFromPreferences =
    questionsQuery.data?.answeredFromPreferences ?? [];

  const saveMutation = useMutation({
    mutationFn: (request: PathwayProfileRequest) =>
      updatePathwayProfile(request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: PATHWAY_KEY });
    },
  });

  const initialAnswers = useMemo(
    () =>
      editing && myYoma && questions
        ? answersFromProfile(myYoma.profile, questions)
        : emptyAnswers(),
    [editing, myYoma, questions],
  );

  const setMyYoma = useCallback(
    (next: MyYoma) => queryClient.setQueryData(MY_YOMA_KEY, next),
    [queryClient],
  );

  const onFinished = useCallback(() => {
    setBuilding(false);
    setEditing(false);
    setAnimateEntrance(true);
    window.scrollTo({ top: 0 });
  }, []);

  const onLock = useCallback(
    async (opportunityId: string, locked: boolean) => {
      setMyYoma(await setPathwayStepLocked(opportunityId, locked));
    },
    [setMyYoma],
  );
  const onSwap = useCallback(
    async (opportunityId: string, replacementOpportunityId: string) => {
      setMyYoma(await swapPathwayStep(opportunityId, replacementOpportunityId));
    },
    [setMyYoma],
  );
  const onStepEvent = useCallback(
    (opportunityId: string, event: PathwayStepEventType) =>
      void recordPathwayStepEvent({ opportunityId, event }),
    [],
  );
  const onChangeAnswers = useCallback(() => {
    saveMutation.reset();
    setAnimateEntrance(false);
    setEditing(true);
    window.scrollTo({ top: 0 });
  }, [saveMutation]);

  if (myYomaQuery.isLoading)
    return (
      <MyYomaShell stage="path">
        <ListPageSkeleton />
      </MyYomaShell>
    );

  if (myYomaQuery.isError && !building)
    return (
      <MyYomaShell stage="path">
        <LoadError
          error={myYomaQuery.error}
          onRetry={() => void myYomaQuery.refetch()}
        />
      </MyYomaShell>
    );

  if (inFlow) {
    if (questionsQuery.isError || !questions)
      return (
        <MyYomaShell stage="onboarding">
          {questionsQuery.isError ? (
            <LoadError
              error={questionsQuery.error}
              onRetry={() => void questionsQuery.refetch()}
            />
          ) : (
            <ListPageSkeleton />
          )}
        </MyYomaShell>
      );

    const saved = saveMutation.isSuccess;
    const pathLoaded = saved && !!myYoma && !myYomaQuery.isFetching;
    const pathFailed =
      saved && !myYomaQuery.isFetching && (myYomaQuery.isError || !myYoma);

    return (
      <MyYomaShell stage="firstVisit">
        <FirstVisitFlow
          key={editing ? "edit" : "new"}
          questions={questions}
          answeredFromPreferences={answeredFromPreferences}
          initialAnswers={initialAnswers}
          startAt={editing ? "questions" : "intro"}
          yoId={{
            displayName:
              myYoma?.yoId.displayName ??
              userProfile?.displayName ??
              userProfile?.firstName ??
              null,
            completedOpportunities:
              myYoma?.yoId.completedOpportunities ??
              userProfile?.opportunityCountCompleted ??
              null,
          }}
          onSubmit={(request) => {
            setBuilding(true);
            saveMutation.mutate(request);
          }}
          isSaving={saveMutation.isPending}
          saveError={
            saveMutation.isError ? (
              <ApiErrors error={saveMutation.error} />
            ) : undefined
          }
          ready={pathLoaded}
          buildError={
            pathFailed ? (
              myYomaQuery.isError ? (
                <ApiErrors error={myYomaQuery.error} />
              ) : (
                "We saved your answers but could not build your path yet."
              )
            ) : undefined
          }
          onRetryBuild={() => void myYomaQuery.refetch()}
          onFinished={onFinished}
          onCancel={
            editing
              ? () => {
                  setEditing(false);
                  setBuilding(false);
                }
              : undefined
          }
        />
      </MyYomaShell>
    );
  }

  return (
    <MyYomaShell stage="path">
      {myYoma && (
        <MyYomaMapView
          myYoma={myYoma}
          animateEntrance={animateEntrance}
          onLock={onLock}
          loadAlternatives={getPathwayStepAlternatives}
          onSwap={onSwap}
          onStepEvent={onStepEvent}
          onChangeAnswers={onChangeAnswers}
          journeySaved={journeySaved.saved}
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
