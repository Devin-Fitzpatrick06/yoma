import { type GetServerSideProps } from "next";
import Head from "next/head";
import type { ReactElement, ReactNode } from "react";
import { Header } from "~/components/Common/Header";
import DropOffSlot from "~/components/CourseQuality/DropOffSlot";
import { collectingDropOff } from "~/components/CourseQuality/DropOffSlot.model";
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
// admin pathway health panel and drop-off slot (W5). Available in development and when the
// Course Quality demo is on (NEXT_PUBLIC_COURSE_QUALITY_DEMO=true); a 404 otherwise.
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
    </div>
  </>
);

ZiqiAdminPreview.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

export default ZiqiAdminPreview;
