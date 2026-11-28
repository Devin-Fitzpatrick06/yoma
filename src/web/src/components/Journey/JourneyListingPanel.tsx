import { useRouter } from "next/router";
import { useEffect, useMemo, useState } from "react";
import {
  finalCopy,
  isJourneyComplete,
  leavingCopy,
  listingUrl,
  parseJourneyParams,
  pathFinderLink,
  SAVE_HINT,
  ziqiThreadSteps,
} from "~/lib/journey";
import { HandoffCard } from "./HandoffCard";
import { JOURNEY_CONFIG } from "./journeyConfig";
import { YoidThread } from "./YoidThread";

// ─────────────────────────────────────────────────────────────────────────────
// Phase 1 on the journey listing (the Yoma listing of `opp-job-001`), shown above the header card
// by `OpportunityPublicDetails` only when the journey is on and this is that listing.
// - Before saving: the thread (Saved Opportunity in progress) and a one-line hint.
// - Saved (Yoma's own save state): the thread (recorded) and the leaving card, whose button is
//   contract link 1 to PathFinder, with `return` = this listing's absolute URL.
// - Opened by YoCV (`journey=demo&from=yocv&application=app-001`): the final card and the thread
//   with all four recorded.
// ─────────────────────────────────────────────────────────────────────────────

/** The panel's markup, from plain props: the listing page and the demo preview both use it. */
export const JourneyListingView: React.FC<{
  saved: boolean;
  complete: boolean;
  yoid: string;
  /** Contract link 1; the leaving card has no button without it. */
  pathfinderHref: string | null;
  headingLevel?: 2 | 3;
}> = ({ saved, complete, yoid, pathfinderHref, headingLevel = 2 }) => {
  const steps = ziqiThreadSteps({ saved, complete });
  return (
    <div data-journey-listing="" className="flex w-full min-w-0 flex-col gap-3">
      {complete ? (
        <HandoffCard copy={finalCopy()} headingLevel={headingLevel} />
      ) : (
        saved && (
          <HandoffCard
            copy={leavingCopy(1)}
            href={pathfinderHref}
            headingLevel={headingLevel}
          />
        )
      )}
      <YoidThread steps={steps} yoid={yoid} />
      {!complete && !saved && (
        <p className="text-gray-dark px-1 text-sm">{SAVE_HINT}</p>
      )}
    </div>
  );
};

export const JourneyListingPanel: React.FC<{
  opportunityId: string;
  /** The signed-in user's saved state, as the listing's own Save button shows it. */
  saved: boolean;
}> = ({ opportunityId, saved }) => {
  const router = useRouter();
  const params = useMemo(
    () => parseJourneyParams(router.query),
    [router.query],
  );

  // the absolute listing URL needs the browser's origin: set after mount, so the server render
  // and the first client render agree
  const [returnUrl, setReturnUrl] = useState<string | null>(null);
  useEffect(() => {
    setReturnUrl(listingUrl(window.location.origin, opportunityId));
  }, [opportunityId]);

  const pathfinderHref = useMemo(
    () =>
      returnUrl
        ? pathFinderLink({
            pathfinderUrl: JOURNEY_CONFIG.pathfinderUrl,
            yoid: JOURNEY_CONFIG.yoid,
            returnUrl,
          })
        : null,
    [returnUrl],
  );

  return (
    <JourneyListingView
      saved={saved}
      complete={isJourneyComplete(params)}
      yoid={JOURNEY_CONFIG.yoid}
      pathfinderHref={pathfinderHref}
    />
  );
};

export default JourneyListingPanel;
