import { type GetServerSideProps } from "next";
import Head from "next/head";
import { type ReactElement } from "react";
import MainLayout from "~/components/Layout/Main";
import LearnerCard from "~/components/Opportunity/LearnerCard";
import { LEARNER_CARD_FIXTURES } from "~/components/Opportunity/LearnerCard.fixtures";
import { PageBackground } from "~/components/PageBackground";
import { env } from "~/env.mjs";
import type { NextPageWithLayout } from "~/pages/_app";

// Development only: every learner card state, without the API. A 404 in production.
export const getServerSideProps: GetServerSideProps = async () => {
  if (env.NODE_ENV === "production") return { notFound: true };
  return { props: {} };
};

const CourseQualityPreview: NextPageWithLayout = () => (
  <>
    <Head>
      <title>Learner card preview</title>
      <meta name="robots" content="noindex" />
    </Head>

    <PageBackground />

    <div className="z-10 container mt-20 max-w-7xl overflow-hidden px-2 py-4">
      <h1 className="font-nunito py-6 text-xl font-black text-white">
        Learner card preview
      </h1>

      <div className="flex flex-col gap-4">
        {LEARNER_CARD_FIXTURES.map(({ name, card }) => (
          // the same white card and padding as the opportunity header
          <div
            key={name}
            className="rounded-[20px] bg-white px-5 py-5 shadow-lg md:px-8 md:py-7"
          >
            <h2 className="text-gray-dark text-sm font-semibold">{name}</h2>
            <LearnerCard card={card} />
          </div>
        ))}
      </div>
    </div>
  </>
);

CourseQualityPreview.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

export default CourseQualityPreview;
