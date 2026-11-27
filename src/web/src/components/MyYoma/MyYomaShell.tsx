import Head from "next/head";
import type { ReactNode } from "react";
import {
  ListPageBody,
  ListPageHeader,
  ListPageShell,
} from "~/components/Common/ListPage/ListPageHeader";
import { SignInButton } from "~/components/SignInButton";

// ─────────────────────────────────────────────────────────────────────────────
// The frame shared by the My Yoma page and its preview: the purple banner with the title, then
// the content column (or, for the first visit, a full-screen frame). Plus the signed-out explanation.
// ─────────────────────────────────────────────────────────────────────────────

export type MyYomaStage = "signedOut" | "onboarding" | "path" | "firstVisit";

const TITLES: Record<
  Exclude<MyYomaStage, "firstVisit">,
  { title: string; description: string }
> = {
  signedOut: {
    title: "My Yoma",
    description: "Your own path from where you are now to the work you want.",
  },
  onboarding: {
    title: "Build your path",
    description:
      "A few quick questions. We use your answers to choose courses on Yoma for you.",
  },
  path: {
    title: "Your world, your Yoma",
    description: "Your path, what you have done so far and where it leads.",
  },
};

export const MyYomaShell: React.FC<{
  stage: MyYomaStage;
  children: ReactNode;
}> = ({ stage, children }) =>
  stage === "firstVisit" ? (
    // the first visit is full screen: no banner, the flow draws its own background
    <>
      <Head>
        <title>Yoma | My Yoma</title>
      </Head>
      <div className="flex w-full flex-col">{children}</div>
    </>
  ) : (
    <>
      <Head>
        <title>Yoma | My Yoma</title>
      </Head>
      <ListPageShell>
        <ListPageHeader
          title={TITLES[stage].title}
          description={TITLES[stage].description}
        />
        <ListPageBody>{children}</ListPageBody>
      </ListPageShell>
    </>
  );

export const MyYomaSignedOut: React.FC = () => (
  <section
    aria-labelledby="my-yoma-signed-out"
    className="mx-auto flex w-full max-w-2xl flex-col items-center gap-3 rounded-xl bg-white p-6 text-center shadow md:p-10"
  >
    <span
      aria-hidden="true"
      className="flex h-12 w-12 items-center justify-center rounded-full bg-white text-2xl shadow-lg"
    >
      🧭
    </span>
    <h2
      id="my-yoma-signed-out"
      className="font-nunito text-xl font-black text-black"
    >
      A path built around you
    </h2>
    <p className="text-gray-dark max-w-prose text-sm md:text-base">
      Tell us where you want to be, where you are starting from and how much
      time you have. My Yoma lines up courses on Yoma in order, each one adding
      a skill the next one uses, and ends with open jobs you can apply for. What
      you complete is added to your YoID.
    </p>
    <SignInButton
      className="btn-sm mt-2 rounded-full"
      showLabel={true}
      callbackUrl="/my-yoma"
    />
  </section>
);
