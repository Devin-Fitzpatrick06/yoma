import { type GetServerSideProps } from "next";
import Head from "next/head";
import { useRouter } from "next/router";
import { useEffect, useState, type ReactElement } from "react";
import type { DraftCheckField } from "~/api/models/courseQuality";
import {
  DRAFT_CHECK_FIXTURES,
  PREVIEW_PARTNER_BADGES,
} from "~/components/CourseQuality/DraftCheckPanel.fixtures";
import {
  DraftCheckPanelView,
  focusDraftField,
  type DraftCheckState,
} from "~/components/CourseQuality/DraftCheckPanel";
import type { ApplicableDraftField } from "~/components/CourseQuality/DraftCheckPanel.helpers";
import MainLayout from "~/components/Layout/Main";
import PartnerBadgeCard from "~/components/MyYoma/PartnerBadgeCard";
import { PartnerBadgeLevelChip } from "~/components/Opportunity/PartnerBadgeChip";
import { PageBackground } from "~/components/PageBackground";
import { env } from "~/env.mjs";
import type { NextPageWithLayout } from "~/pages/_app";

// ─────────────────────────────────────────────────────────────────────────────
// The Ziqi check panel (W1) and the partner badges (W2) with example answers, without the API or a
// sign-in. Available in development and when the Course Quality demo is on
// (NEXT_PUBLIC_COURSE_QUALITY_DEMO=true); a 404 otherwise.
// `?result=notVerified|needsLook|verified|empty|checking|error`, `&drawer=open` opens the phone drawer.
// ─────────────────────────────────────────────────────────────────────────────

export const getServerSideProps: GetServerSideProps = async () => {
  if (
    env.NODE_ENV === "production" &&
    env.NEXT_PUBLIC_COURSE_QUALITY_DEMO !== true
  )
    return { notFound: true };
  return { props: {} };
};

type ResultKey = keyof typeof DRAFT_CHECK_FIXTURES;
type View = ResultKey | "empty" | "checking" | "error";

const VIEWS: { key: View; label: string }[] = [
  { key: "notVerified", label: "Not verified yet" },
  { key: "needsLook", label: "Needs a look" },
  { key: "verified", label: "Verified" },
  { key: "empty", label: "Empty form" },
  { key: "checking", label: "Checking" },
  { key: "error", label: "Could not check" },
];

const stateFor = (view: View): DraftCheckState => {
  if (view === "empty") return { status: "empty" };
  if (view === "checking") return { status: "checking", result: null };
  if (view === "error") return { status: "error", result: null };
  return { status: "ready", result: DRAFT_CHECK_FIXTURES[view] };
};

const inputClass =
  "input border-gray focus:border-gray w-full rounded-md focus:outline-none";

const ZiqiDraftPreview: NextPageWithLayout = () => {
  const router = useRouter();
  const [view, setView] = useState<View>("notVerified");
  const [values, setValues] = useState({
    title: "Budgeting basics",
    summary: "",
    uRL: "example.com/budgeting",
    description: "",
  });

  useEffect(() => {
    const requested = router.query.result;
    if (VIEWS.some((v) => v.key === requested)) setView(requested as View);
  }, [router.query.result]);

  const onApply = (field: ApplicableDraftField, value: string) =>
    setValues((prev) => ({ ...prev, [field]: value }));
  const onGoTo = (field: DraftCheckField) => focusDraftField(field);

  return (
    <>
      <Head>
        <title>Ziqi check preview</title>
        <meta name="robots" content="noindex" />
      </Head>
      <PageBackground />

      <div className="z-10 container mt-20 max-w-7xl overflow-clip px-2 py-4 pb-20 xl:pb-4">
        <h3 className="mb-1 font-bold text-white">Ziqi check preview</h3>
        <p className="mb-4 text-xs text-white">
          Example answers, not a real listing. The panel never blocks saving.
        </p>

        <div
          role="group"
          aria-label="Example answer"
          className="mb-4 flex flex-wrap gap-2"
        >
          {VIEWS.map((v) => (
            <button
              key={v.key}
              type="button"
              aria-pressed={view === v.key}
              onClick={() => setView(v.key)}
              className={`btn btn-xs border-none ${
                view === v.key
                  ? "bg-white text-black"
                  : "bg-purple-shade text-white"
              }`}
            >
              {v.label}
            </button>
          ))}
        </div>

        <div className="flex flex-col gap-4 xl:flex-row">
          {/* A stand-in for the listing form, so "Go to" and "Apply" have fields to reach */}
          <form
            className="shadow-custom flex min-w-0 grow flex-col gap-4 rounded-lg bg-white p-4 md:p-8"
            onSubmit={(e) => e.preventDefault()}
          >
            <h5 className="font-bold tracking-wider">General</h5>
            <label className="flex flex-col gap-1 text-sm font-semibold">
              Title
              <input
                className={inputClass}
                name="title"
                value={values.title}
                onChange={(e) =>
                  setValues((p) => ({ ...p, title: e.target.value }))
                }
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold">
              Link
              <input
                className={inputClass}
                name="uRL"
                value={values.uRL}
                onChange={(e) =>
                  setValues((p) => ({ ...p, uRL: e.target.value }))
                }
              />
            </label>
            <label className="flex flex-col gap-1 text-sm font-semibold">
              Summary
              <textarea
                className="textarea border-gray w-full rounded-md"
                name="summary"
                value={values.summary}
                onChange={(e) =>
                  setValues((p) => ({ ...p, summary: e.target.value }))
                }
              />
            </label>
            <div className="flex flex-col gap-4 md:flex-row">
              <label className="flex grow flex-col gap-1 text-sm font-semibold">
                Start date
                <input
                  type="date"
                  className={inputClass}
                  id="input_dateStart"
                />
              </label>
              <label className="flex grow flex-col gap-1 text-sm font-semibold">
                End date
                <input type="date" className={inputClass} id="input_dateEnd" />
              </label>
            </div>
            <label className="flex flex-col gap-1 text-sm font-semibold">
              Skills
              <input
                className={inputClass}
                id="input_skills"
                placeholder="Select skills..."
              />
            </label>

            <section className="border-gray mt-4 flex flex-col gap-3 border-t pt-4">
              <h5 className="font-bold tracking-wider">Partner badges (W2)</h5>
              <p className="text-gray-dark flex flex-wrap items-center gap-2 text-sm">
                On course cards:
                <PartnerBadgeLevelChip level="Platinum" />
                <PartnerBadgeLevelChip level="Gold" />
                <PartnerBadgeLevelChip level="Silver" />
                <PartnerBadgeLevelChip level="Bronze" />
              </p>
              {PREVIEW_PARTNER_BADGES.map((badge) => (
                <PartnerBadgeCard key={badge.organizationId} badge={badge} />
              ))}
            </section>
          </form>

          <div className="contents xl:block xl:w-80 xl:flex-none">
            <DraftCheckPanelView
              key={`${view}-${String(router.query.drawer)}`}
              state={stateFor(view)}
              defaultExpanded={router.query.drawer === "open"}
              onGoToField={onGoTo}
              onApplySuggestion={onApply}
            />
          </div>
        </div>
      </div>
    </>
  );
};

ZiqiDraftPreview.getLayout = function getLayout(page: ReactElement) {
  return <MainLayout>{page}</MainLayout>;
};

export default ZiqiDraftPreview;
