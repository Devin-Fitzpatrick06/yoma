import { useQuery } from "@tanstack/react-query";
import { useAtomValue } from "jotai";
import { useSession } from "next-auth/react";
import { useRouter } from "next/router";
import { useEffect, useState } from "react";
import { getPathwayProfile } from "~/api/services/pathway";
import { env } from "~/env.mjs";
import { ROLE_ADMIN, ROLE_ORG_ADMIN } from "~/lib/constants";
import { hasSkippedSettingsDialogAtom, userProfileAtom } from "~/lib/store";
import {
  isUserProfileCompleted,
  isUserSettingsConfigured,
} from "~/lib/utils/profile";
import { FIRST_VISIT_REDIRECT_KEY, shouldRedirectToMyYoma } from "./firstVisit";

// ─────────────────────────────────────────────────────────────────────────────
// The home page's first-visit redirect (iteration 4, W7): a signed-in youth with the switch on and
// no pathway profile goes to /my-yoma, once per browser session. It waits for Yoma's own profile
// and settings dialogs (components/Global.tsx) to be done, and "Go to Yoma home" (`/?home=1`)
// opts out for the session. The decision is `shouldRedirectToMyYoma`.
// ─────────────────────────────────────────────────────────────────────────────

const readHandled = (): boolean => {
  try {
    return window.sessionStorage.getItem(FIRST_VISIT_REDIRECT_KEY) === "1";
  } catch {
    return false;
  }
};

const markHandled = () => {
  try {
    window.sessionStorage.setItem(FIRST_VISIT_REDIRECT_KEY, "1");
  } catch {
    // storage blocked: the in-memory flag below still stops a second redirect
  }
};

export const useFirstVisitRedirect = (): void => {
  const router = useRouter();
  const { data: session, status } = useSession();
  const userProfile = useAtomValue(userProfileAtom);
  const hasSkippedSettings = useAtomValue(hasSkippedSettingsDialogAtom);
  // null until read on the client, so the server render and the first client render agree
  const [handled, setHandled] = useState<boolean | null>(null);

  const optedOut = router.isReady && router.query.home !== undefined;

  useEffect(() => {
    if (!env.NEXT_PUBLIC_PATHWAY_ENABLED || !router.isReady) return;
    if (optedOut) markHandled();
    setHandled(optedOut || readHandled());
  }, [optedOut, router.isReady]);

  const roles = session?.user?.roles ?? [];
  const isAdminOrPartner =
    roles.includes(ROLE_ADMIN) || roles.includes(ROLE_ORG_ADMIN);
  const profileCompleted = isUserProfileCompleted(userProfile);
  const settingsDialogPending =
    !!userProfile &&
    !hasSkippedSettings &&
    !isUserSettingsConfigured(userProfile);

  const eligible =
    env.NEXT_PUBLIC_PATHWAY_ENABLED &&
    status === "authenticated" &&
    !isAdminOrPartner &&
    profileCompleted === true &&
    !settingsDialogPending &&
    handled === false;

  const profileQuery = useQuery({
    queryKey: ["pathway", "profile", "firstVisit"],
    queryFn: getPathwayProfile,
    enabled: eligible,
    retry: false,
    staleTime: 5 * 60 * 1000,
  });

  const hasPathway = profileQuery.isSuccess ? profileQuery.data !== null : null;

  useEffect(() => {
    if (
      !shouldRedirectToMyYoma({
        pathwayEnabled: env.NEXT_PUBLIC_PATHWAY_ENABLED,
        sessionStatus: status,
        isAdminOrPartner,
        profileCompleted,
        settingsDialogPending,
        alreadyHandled: handled !== false,
        hasPathway,
      })
    )
      return;
    markHandled();
    setHandled(true);
    void router.replace("/my-yoma");
  }, [
    hasPathway,
    handled,
    isAdminOrPartner,
    profileCompleted,
    router,
    settingsDialogPending,
    status,
  ]);
};
