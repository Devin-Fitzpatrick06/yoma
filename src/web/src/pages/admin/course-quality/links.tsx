import { type GetServerSidePropsContext } from "next";
import { getServerSession } from "next-auth";
import Head from "next/head";
import Link from "next/link";
import type { ReactElement } from "react";
import { IoArrowBack } from "react-icons/io5";
import { checkLinkHealth } from "~/api/services/linkHealth";
import {
  ListPageBody,
  ListPageHeader,
  ListPageShell,
} from "~/components/Common/ListPage/ListPageHeader";
import LinkHealthChecker from "~/components/CourseQuality/LinkHealthChecker";
import LinkHealthGuide from "~/components/CourseQuality/LinkHealthGuide";
import MainLayout from "~/components/Layout/Main";
import { InternalServerError } from "~/components/Status/InternalServerError";
import { Unauthenticated } from "~/components/Status/Unauthenticated";
import { Unauthorized } from "~/components/Status/Unauthorized";
import { ROLE_ADMIN, THEME_BLUE } from "~/lib/constants";
import { type NextPageWithLayout } from "~/pages/_app";
import { authOptions } from "~/server/auth";

// ─────────────────────────────────────────────────────────────────────────────
// Link health (Ziqi iteration 5): check links from any platform (Yoma, YouthHub, 1MiO, …) through
// the API's safe link checker, 200 at a time, and download the result as CSV.
// ─────────────────────────────────────────────────────────────────────────────

const QUEUE_PATH = "/admin/course-quality";

// ⚠️ SSR
export async function getServerSideProps(context: GetServerSidePropsContext) {
  const session = await getServerSession(context.req, context.res, authOptions);
  if (!session) return { props: { error: 401 } };
  if (!session.user?.roles?.includes(ROLE_ADMIN))
    return { props: { error: 403 } };
  return { props: { error: null } };
}

const CourseQualityLinkHealth: NextPageWithLayout<{ error?: number }> = ({
  error,
}) => {
  if (error === 401) return <Unauthenticated />;
  if (error === 403) return <Unauthorized />;
  if (error) return <InternalServerError />;

  return (
    <>
      <Head>
        <title>Yoma | Ziqi · Link health</title>
      </Head>
      <ListPageShell>
        <ListPageHeader
          title="Link health"
          description="Check whether links still work, for Yoma or any other platform. Paste them or upload a file; up to 200 are checked at a time."
        >
          <Link
            href={QUEUE_PATH}
            className="text-gray-dark inline-flex items-center gap-1 text-sm font-semibold hover:underline"
          >
            <IoArrowBack aria-hidden="true" className="h-4 w-4" />
            Reviewer queue
          </Link>
        </ListPageHeader>
        <ListPageBody>
          <LinkHealthChecker check={checkLinkHealth} />
          <LinkHealthGuide />
        </ListPageBody>
      </ListPageShell>
    </>
  );
};

CourseQualityLinkHealth.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

CourseQualityLinkHealth.theme = function getTheme() {
  return THEME_BLUE;
};

export default CourseQualityLinkHealth;
