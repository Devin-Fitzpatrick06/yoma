import { type GetServerSideProps } from "next";
import Head from "next/head";
import { useRouter } from "next/router";
import { useEffect, useState, type ReactElement } from "react";
import MainLayout from "~/components/Layout/Main";
import FirstVisitFlow from "~/components/MyYoma/FirstVisitFlow";
import { isPreviewAllowed } from "~/components/MyYoma/firstVisit";
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
// `?view=partner` the partner badges.
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
  | "partner";

const VIEWS: View[] = ["onboarding", "questions", "journey", "path", "partner"];

/** The preview's stand-in for saving: a short wait, as the API would take. */
const SAVE_MS = 600;

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
      {view === "partner" ? (
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
