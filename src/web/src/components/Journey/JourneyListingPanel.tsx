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
  ziqiVerdictLine,
} from "~/lib/journey";
import { HandoffCard } from "./HandoffCard";
import { JOURNEY_CONFIG } from "./journeyConfig";
import { YoidThread } from "./YoidThread";

// ─────────────────────────────────────────────────────────────────────────────
// Phase 1 on the journey listing (the Yoma listing of `opp-job-001`), shown above the header card
// by `OpportunityPublicDetails` only when the journey is on and this is that listing.
// - Always: Ziqi's verdict on the listing ("Checked by Ziqi: Verified · Grade A") inside the thread
//   card, from the listing's public card (GET /courseQuality/opportunity/{id}/card, which the page
//   already loads); hidden when there is no card. A Job's card needs CourseQuality:Jobs:ShowInBrowse.
// - Before saving: the thread (Saved Opportunity in progress) and a one-line hint, in the card.
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
  /** Ziqi's verdict line (`ziqiVerdictLine`); hidden when null. */
  verdict?: string | null;
}> = ({
  saved,
  complete,
  yoid,
  pathfinderHref,
  headingLevel = 2,
  verdict = null,
}) => {
  const steps = ziqiThreadSteps({ saved, complete });
  const showHint = !complete && !saved;
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
      {/* the verdict and the hint sit inside the white thread card: on the page's purple header
          band, gray text would be unreadable */}
      <YoidThread steps={steps} yoid={yoid}>
        {(verdict || showHint) && (
          <div className="border-gray-light flex flex-col gap-1 border-t pt-3">
            {verdict && (
              <p
                data-journey-verdict=""
                className="text-purple text-sm font-semibold"
              >
                {verdict}
              </p>
            )}
            {showHint && <p className="text-gray-dark text-sm">{SAVE_HINT}</p>}
          </div>
        )}
      </YoidThread>
    </div>
  );
};

export const JourneyListingPanel: React.FC<{
  opportunityId: string;
  /** The signed-in user's saved state, as the listing's own Save button shows it. */
  saved: boolean;
  /** The listing's public course-quality card, as the page already loads it; null without one. */
  card?: { status: string; grade: string | null } | null;
}> = ({ opportunityId, saved, card = null }) => {
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
      verdict={ziqiVerdictLine(card)}
    />
  );
};

export default JourneyListingPanel;
