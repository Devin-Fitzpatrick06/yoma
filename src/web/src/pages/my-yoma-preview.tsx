import { type GetServerSideProps } from "next";
import Head from "next/head";
import { useRouter } from "next/router";
import { useCallback, useEffect, useState, type ReactElement } from "react";
import type {
  MyYoma,
  PathwayCourse,
  PathwayStepEventType,
} from "~/api/models/pathway";
import MainLayout from "~/components/Layout/Main";
import FirstVisitFlow from "~/components/MyYoma/FirstVisitFlow";
import { isPreviewAllowed } from "~/components/MyYoma/firstVisit";
import JourneyMap from "~/components/MyYoma/JourneyMap/JourneyMap";
import {
  completeNextStep,
  JOURNEY_MAP_FIXTURE,
  journeyAlternativesFor,
  lockStep,
  swapStep,
} from "~/components/MyYoma/JourneyMap/journeyMap.fixtures";
import {
  MY_YOMA_FIXTURE,
  PARTNER_BADGE_FIXTURES,
  QUESTIONS_FIXTURE,
  QUESTIONS_FROM_PREFERENCES_FIXTURE,
} from "~/components/MyYoma/myYoma.fixtures";
import MyYomaMapView from "~/components/MyYoma/MyYomaMapView";
import MyYomaView from "~/components/MyYoma/MyYomaView";
import { MyYomaShell } from "~/components/MyYoma/MyYomaShell";
import PartnerBadgeCard from "~/components/MyYoma/PartnerBadgeCard";
import PathwayOnboarding from "~/components/MyYoma/PathwayOnboarding";
import { env } from "~/env.mjs";
import { answersFromProfile, emptyAnswers } from "~/lib/pathway";
import type { NextPageWithLayout } from "~/pages/_app";

// ─────────────────────────────────────────────────────────────────────────────
// My Yoma with example data, without the API or a sign-in. Available when the Course Quality
// demo is on (NEXT_PUBLIC_COURSE_QUALITY_DEMO=true), or in development with
// NEXT_PUBLIC_PATHWAY_ENABLED on; a 404 otherwise.
// `?view=onboarding` the first visit (preferences already answered time and skills), then the map;
// `?view=questions` the plain slides with every question; `?view=path` the iteration-3 path;
// `?view=partner` the partner badges; `?view=map` the journey map alone (lock, swap, celebration).
// ─────────────────────────────────────────────────────────────────────────────

export const getServerSideProps: GetServerSideProps = async () => {
  if (
    !isPreviewAllowed({
      production: env.NODE_ENV === "production",
      demo: env.NEXT_PUBLIC_COURSE_QUALITY_DEMO === true,
      pathwayEnabled: env.NEXT_PUBLIC_PATHWAY_ENABLED === true,
    })
  )
    return { notFound: true };
  return { props: {} };
};

type View =
  | "onboarding"
  | "questions"
  | "edit"
  | "journey"
  | "path"
  | "partner"
  | "map";

const VIEWS: View[] = [
  "onboarding",
  "questions",
  "journey",
  "path",
  "partner",
  "map",
];

/** The preview's stand-in for saving: a short wait, as the API would take. */
const SAVE_MS = 600;

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

// The journey map with fixtures: lock, swap and step events act on local state, with a short
// delay standing in for the API. "Replay entrance" remounts the map; "Complete next step" shows the
// celebration.
const JourneyMapPreview: React.FC<{ onChangeAnswers: () => void }> = ({
  onChangeAnswers,
}) => {
  const [myYoma, setMyYoma] = useState<MyYoma>(JOURNEY_MAP_FIXTURE);
  const [run, setRun] = useState(0);
  const [events, setEvents] = useState<string[]>([]);
  const [alternatives, setAlternatives] = useState<
    Record<string, PathwayCourse>
  >({});

  const onStepEvent = useCallback(
    (opportunityId: string, event: PathwayStepEventType) =>
      setEvents((e) => [`${event} · ${opportunityId}`, ...e].slice(0, 6)),
    [],
  );
  const loadAlternatives = useCallback(
    async (opportunityId: string) => {
      await wait(700);
      const course = myYoma.stages
        .flatMap((s) => s.courses)
        .find((c) => c.opportunityId === opportunityId);
      const list = course ? journeyAlternativesFor(course) : [];
      setAlternatives((a) => ({
        ...a,
        ...Object.fromEntries(list.map((c) => [c.opportunityId, c])),
      }));
      return list;
    },
    [myYoma],
  );

  return (
    <div className="flex w-full min-w-0 flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2 text-xs">
        <span className="text-gray-dark font-semibold">Preview controls:</span>
        <button
          type="button"
          className="btn btn-xs rounded-full"
          onClick={() => {
            setMyYoma(JOURNEY_MAP_FIXTURE);
            setRun((r) => r + 1);
          }}
        >
          Replay entrance
        </button>
        <button
          type="button"
          className="btn btn-xs rounded-full"
          onClick={() => setMyYoma((m) => completeNextStep(m))}
        >
          Complete next step
        </button>
        {events.length > 0 && (
          <span className="text-gray-dark min-w-0 truncate">
            Last event: {events[0]}
          </span>
        )}
      </div>
      <JourneyMap
        key={run}
        myYoma={myYoma}
        animateEntrance={true}
        onLock={async (opportunityId, locked) => {
          await wait(300);
          setMyYoma((m) => lockStep(m, opportunityId, locked));
        }}
        loadAlternatives={loadAlternatives}
        onSwap={async (opportunityId, replacementOpportunityId) => {
          await wait(400);
          const replacement = alternatives[replacementOpportunityId];
          if (!replacement) throw new Error("Unknown alternative");
          setMyYoma((m) => swapStep(m, opportunityId, replacement));
        }}
        onStepEvent={onStepEvent}
        onChangeAnswers={onChangeAnswers}
      />
    </div>
  );
};

const MyYomaPreview: NextPageWithLayout = () => {
  const router = useRouter();
  const [view, setView] = useState<View>("onboarding");
  const [saving, setSaving] = useState(false);
  const [ready, setReady] = useState(false);
  const [myYoma, setMyYoma] = useState(MY_YOMA_FIXTURE);

  useEffect(() => {
    const requested = router.query.view;
    if (typeof requested === "string" && VIEWS.includes(requested as View))
      setView(requested as View);
  }, [router.query.view]);

  const fakeSave = () => {
    setSaving(true);
    setReady(false);
    window.setTimeout(() => {
      setSaving(false);
      setReady(true);
    }, SAVE_MS);
  };

  const journey = (
    <MyYomaShell stage="path">
      <MyYomaMapView
        myYoma={myYoma}
        animateEntrance={true}
        onLock={async (id, locked) =>
          setMyYoma((current) => ({
            ...current,
            stages: current.stages.map((stage) => ({
              ...stage,
              courses: stage.courses.map((c) =>
                c.opportunityId === id ? { ...c, locked } : c,
              ),
            })),
          }))
        }
        loadAlternatives={async () => []}
        onSwap={async () => undefined}
        onStepEvent={() => undefined}
        onChangeAnswers={() => setView("edit")}
      />
    </MyYomaShell>
  );

  return (
    <>
      <Head>
        <meta name="robots" content="noindex" />
      </Head>
      {view === "map" ? (
        <MyYomaShell stage="path">
          <JourneyMapPreview onChangeAnswers={() => setView("edit")} />
        </MyYomaShell>
      ) : view === "partner" ? (
        <MyYomaShell stage="path">
          <div className="mx-auto flex w-full max-w-4xl flex-col gap-4">
            {PARTNER_BADGE_FIXTURES.map((badge) => (
              <PartnerBadgeCard key={badge.organizationId} badge={badge} />
            ))}
          </div>
        </MyYomaShell>
      ) : view === "path" ? (
        <MyYomaShell stage="path">
          <MyYomaView
            myYoma={MY_YOMA_FIXTURE}
            onChangeAnswers={() => setView("edit")}
          />
        </MyYomaShell>
      ) : view === "journey" ? (
        journey
      ) : view === "questions" ? (
        <MyYomaShell stage="onboarding">
          <PathwayOnboarding
            questions={QUESTIONS_FIXTURE.questions}
            initialAnswers={emptyAnswers()}
            onSubmit={() => setView("journey")}
          />
        </MyYomaShell>
      ) : (
        <MyYomaShell stage="firstVisit">
          <FirstVisitFlow
            key={view}
            questions={QUESTIONS_FROM_PREFERENCES_FIXTURE.questions}
            answeredFromPreferences={
              QUESTIONS_FROM_PREFERENCES_FIXTURE.answeredFromPreferences
            }
            initialAnswers={
              view === "edit"
                ? answersFromProfile(
                    MY_YOMA_FIXTURE.profile,
                    QUESTIONS_FROM_PREFERENCES_FIXTURE.questions,
                  )
                : emptyAnswers()
            }
            startAt={view === "edit" ? "questions" : "intro"}
            yoId={{
              displayName: MY_YOMA_FIXTURE.yoId.displayName,
              completedOpportunities:
                MY_YOMA_FIXTURE.yoId.completedOpportunities,
            }}
            onSubmit={fakeSave}
            isSaving={saving}
            ready={ready}
            onFinished={() => setView("journey")}
            onCancel={view === "edit" ? () => setView("journey") : undefined}
          />
        </MyYomaShell>
      )}
    </>
  );
};

MyYomaPreview.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

export default MyYomaPreview;
