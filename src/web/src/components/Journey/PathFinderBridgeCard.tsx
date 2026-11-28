import Link from "next/link";
import { IoArrowForward } from "react-icons/io5";
import { journeyItem } from "~/lib/journey";

// ─────────────────────────────────────────────────────────────────────────────
// The end of the route on My Yoma (demo, NEXT_PUBLIC_JOURNEY_ENABLED): My Yoma maps the route
// within Yoma; PathFinder plans across platforms. The link goes to the journey listing, where
// saving hands the Saved Opportunity on to PathFinder.
// ─────────────────────────────────────────────────────────────────────────────

export const PATHFINDER_BRIDGE_TITLE =
  "Plan it across P2E and WhatsApp with PathFinder";

export const PathFinderBridgeCard: React.FC<{
  saved: boolean;
  listingHref: string;
  headingLevel?: 2 | 3;
}> = ({ saved, listingHref, headingLevel = 2 }) => {
  const Heading = headingLevel === 3 ? "h3" : "h2";
  return (
    <section
      data-journey-bridge=""
      className="border-blue flex w-full min-w-0 flex-col gap-2 rounded-xl border-l-4 bg-white p-4 shadow-lg md:p-5"
    >
      <Heading className="font-nunito text-purple text-lg font-black break-words md:text-xl">
        {PATHFINDER_BRIDGE_TITLE}
      </Heading>
      <p className="text-gray-dark text-sm md:text-base">
        PathFinder plans your next steps across Passport to Earning, Yoma and
        WhatsApp/SMS, starting from the job you save here.
      </p>
      <Link
        href={listingHref}
        className="text-green inline-flex w-fit items-center gap-1 text-sm font-semibold hover:underline md:text-base"
      >
        {saved ? "Open your saved job" : "Open the job to save it"}
        <span className="sr-only">: {journeyItem(1).title}</span>
        <IoArrowForward aria-hidden="true" className="h-4 w-4" />
      </Link>
    </section>
  );
};

export default PathFinderBridgeCard;
