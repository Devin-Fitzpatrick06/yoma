import { type GetServerSideProps } from "next";
import Head from "next/head";
import { useState, type ReactElement, type ReactNode } from "react";
import type { CourseQualityAssessment } from "~/api/models/courseQuality";
import { Header } from "~/components/Common/Header";
import {
  AI_SUMMARY_FIXTURE,
  AI_USAGE_EMPTY_FIXTURE,
  AI_USAGE_LOCAL_FIXTURE,
  AI_USAGE_MIXED_HISTORY_FIXTURE,
  AI_USAGE_NOT_CONFIGURED_FIXTURE,
  AI_USAGE_REMOTE_PRICED_FIXTURE,
  AI_USAGE_REMOTE_UNPRICED_FIXTURE,
  AI_USAGE_SUMMARY_FIXTURE,
  aiCheckAssessmentFixture,
} from "~/components/CourseQuality/AIUsage.fixtures";
import AIUsageCheck from "~/components/CourseQuality/AIUsageCheck";
import AIUsagePanel from "~/components/CourseQuality/AIUsagePanel";
import DropOffSlot from "~/components/CourseQuality/DropOffSlot";
import { collectingDropOff } from "~/components/CourseQuality/DropOffSlot.model";
import {
  fakeLinkHealthCheck,
  LINK_HEALTH_FIXTURE_URLS,
  LINK_HEALTH_REPORT_FIXTURE,
} from "~/components/CourseQuality/LinkHealth.fixtures";
import LinkHealthChecker from "~/components/CourseQuality/LinkHealthChecker";
import LinkHealthGuide from "~/components/CourseQuality/LinkHealthGuide";
import PathwayHealthPanel from "~/components/CourseQuality/PathwayHealthPanel";
import { PATHWAY_HEALTH_COLLECTING_FIXTURE } from "~/components/CourseQuality/PathwayHealthPanel.fixtures";
import MainLayout from "~/components/Layout/Main";
import { MyPathCard } from "~/components/YoID/MyPathCard";
import {
  MY_PATH_SUMMARY_DONE_FIXTURE,
  MY_PATH_SUMMARY_FIXTURE,
} from "~/components/YoID/MyPathCard.fixtures";
import { env } from "~/env.mjs";
import type { NextPageWithLayout } from "~/pages/_app";

// ─────────────────────────────────────────────────────────────────────────────
// Ziqi iteration 4 fixtures without the API or a sign-in: the YoID "My path" card (W3) and the
// admin pathway health panel and drop-off slot (W5). Iteration 5 adds the reviewer card's AI
// check, the admin AI usage panel and the link health page, with a fake AI run and a fake link
// checker. Available in development and when the Course Quality demo is on
// (NEXT_PUBLIC_COURSE_QUALITY_DEMO=true); a 404 otherwise.
// ─────────────────────────────────────────────────────────────────────────────

export const getServerSideProps: GetServerSideProps = async () => {
  if (
    env.NODE_ENV === "production" &&
    env.NEXT_PUBLIC_COURSE_QUALITY_DEMO !== true
  )
    return { notFound: true };
  return { props: {} };
};

// the same frame the YoID dashboard gives each card
const YoIdCardFrame: React.FC<{ title: string; children: ReactNode }> = ({
  title,
  children,
}) => (
  <div className="flex w-full flex-col gap-2 sm:w-[300px] md:w-[350px] lg:w-[400px]">
    <Header title={title} url="/my-yoma" />
    <div className="flex min-h-[185px] w-full flex-col gap-4 rounded-lg bg-white p-4 shadow">
      {children}
    </div>
  </div>
);

const PreviewSection: React.FC<{
  title: string;
  description: string;
  children: ReactNode;
}> = ({ title, description, children }) => (
  <section className="flex flex-col gap-3">
    <div className="flex flex-col gap-0.5">
      <h2 className="font-nunito text-xl font-black text-black">{title}</h2>
      <p className="text-gray-dark text-sm">{description}</p>
    </div>
    {children}
  </section>
);

const PreviewCard: React.FC<{ label: string; children: ReactNode }> = ({
  label,
  children,
}) => (
  <div className="flex min-w-0 flex-col gap-2 rounded-2xl bg-white p-4 shadow-sm">
    <p className="text-gray-dark text-xs font-semibold tracking-wide uppercase">
      {label}
    </p>
    {children}
  </div>
);

const FAKE_AI_RUN_MS = 4000;

/** "Run AI check" on the preview: waits a few seconds, then answers with the local fixture. */
const fakeAICheck = async (
  opportunityId: string,
): Promise<CourseQualityAssessment> => {
  await new Promise((resolve) => setTimeout(resolve, FAKE_AI_RUN_MS));
  return {
    ...aiCheckAssessmentFixture(
      {
        ...AI_USAGE_LOCAL_FIXTURE,
        durationMs: FAKE_AI_RUN_MS,
        dateRun: new Date().toISOString(),
      },
      AI_SUMMARY_FIXTURE,
    ),
    opportunityId,
  };
};

const RunnableAICheck: React.FC = () => {
  const [item, setItem] = useState(() => aiCheckAssessmentFixture(null, null));
  return (
    <AIUsageCheck
      item={item}
      configuredModel="qwen2.5:3b on localhost:11434"
      run={fakeAICheck}
      onAssessed={setItem}
      headingLevel="h4"
    />
  );
};

const ZiqiAdminPreview: NextPageWithLayout = () => (
  <>
    <Head>
      <title>Yoma | Ziqi preview</title>
      <meta name="robots" content="noindex" />
    </Head>
    <div className="container mx-auto flex w-full max-w-7xl flex-col gap-8 px-4 pt-24 pb-10">
      <p
        role="note"
        className="bg-yellow-tint text-gray-dark rounded-lg px-3 py-2 text-sm"
      >
        Preview with example data. Nothing here is a real youth or pilot result.
      </p>

      <PreviewSection
        title="YoID: My path"
        description="The card on /yoid with a path in progress, a finished path, and no path yet."
      >
        <div className="flex flex-wrap justify-center gap-4 lg:justify-start">
          <YoIdCardFrame title="🧭 My path">
            <MyPathCard summary={MY_PATH_SUMMARY_FIXTURE} />
          </YoIdCardFrame>
          <YoIdCardFrame title="🧭 My path">
            <MyPathCard summary={MY_PATH_SUMMARY_DONE_FIXTURE} />
          </YoIdCardFrame>
          <YoIdCardFrame title="🧭 My path">
            <MyPathCard summary={null} />
          </YoIdCardFrame>
        </div>
      </PreviewSection>

      <PreviewSection
        title="Admin: Pathway health"
        description="On the Ziqi reviewer queue. Drop-off is collected from the pilot; no figure is shown until there is real data."
      >
        <PathwayHealthPanel
          health={PATHWAY_HEALTH_COLLECTING_FIXTURE}
          isLoading={false}
          isError={false}
        />
      </PreviewSection>

      <PreviewSection
        title="Reviewer card: Drop-off slot"
        description="Reserved on each course in reviewer mode; the reviewer search does not send drop-off yet."
      >
        <div className="flex max-w-md flex-col gap-2 rounded-2xl bg-white p-4 shadow-sm">
          <DropOffSlot dropOff={collectingDropOff()} showDetail={true} />
        </div>
      </PreviewSection>

      <PreviewSection
        title="Reviewer card: AI check"
        description="In reviewer mode, under Checks: the model, host, tokens, cost, time and outcome of the run. Run AI check here waits a few seconds and answers with example data."
      >
        <div className="grid gap-4 md:grid-cols-2">
          <PreviewCard label="Local model, answered">
            <AIUsageCheck
              item={aiCheckAssessmentFixture(
                AI_USAGE_LOCAL_FIXTURE,
                AI_SUMMARY_FIXTURE,
              )}
              headingLevel="h4"
            />
          </PreviewCard>
          <PreviewCard label="Remote model with a price">
            <AIUsageCheck
              item={aiCheckAssessmentFixture(
                AI_USAGE_REMOTE_PRICED_FIXTURE,
                AI_SUMMARY_FIXTURE,
              )}
              headingLevel="h4"
            />
          </PreviewCard>
          <PreviewCard label="Remote model, no price, reply not in the format">
            <AIUsageCheck
              item={aiCheckAssessmentFixture(
                AI_USAGE_REMOTE_UNPRICED_FIXTURE,
                null,
              )}
              headingLevel="h4"
            />
          </PreviewCard>
          <PreviewCard label="No AI ran yet: try Run AI check">
            <RunnableAICheck />
          </PreviewCard>
        </div>
      </PreviewSection>

      <PreviewSection
        title="Admin: AI usage"
        description="On the Ziqi reviewer queue: the configured model, totals and recent runs; a local model with older remote runs, one without a price; then no runs yet, and no model configured."
      >
        <AIUsagePanel
          summary={AI_USAGE_SUMMARY_FIXTURE}
          isLoading={false}
          isError={false}
        />
        <AIUsagePanel
          summary={AI_USAGE_MIXED_HISTORY_FIXTURE}
          isLoading={false}
          isError={false}
        />
        <div className="grid gap-4 lg:grid-cols-2">
          <AIUsagePanel
            summary={AI_USAGE_EMPTY_FIXTURE}
            isLoading={false}
            isError={false}
          />
          <AIUsagePanel
            summary={AI_USAGE_NOT_CONFIGURED_FIXTURE}
            isLoading={false}
            isError={false}
          />
        </div>
      </PreviewSection>

      <PreviewSection
        title="Admin: Link health"
        description="/admin/course-quality/links. Check links here too: a fake checker answers from the address alone and fetches nothing."
      >
        <LinkHealthChecker
          check={fakeLinkHealthCheck}
          initialText={LINK_HEALTH_FIXTURE_URLS.join("\n")}
          initialPlatform="YouthHub (example)"
          initialReport={LINK_HEALTH_REPORT_FIXTURE}
        />
        <LinkHealthGuide />
      </PreviewSection>
    </div>
  </>
);

ZiqiAdminPreview.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

export default ZiqiAdminPreview;
