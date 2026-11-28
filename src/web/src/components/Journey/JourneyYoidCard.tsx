import Link from "next/link";
import { IoArrowForward } from "react-icons/io5";
import { journeyItem, ziqiThreadSteps } from "~/lib/journey";
import { YoidThread } from "./YoidThread";

// ─────────────────────────────────────────────────────────────────────────────
// The YoID dashboard's journey card (demo, NEXT_PUBLIC_JOURNEY_ENABLED): the thread in its
// phase 1 state (Saved Opportunity recorded once the youth has saved the journey listing) and a
// link to that listing. The page draws the card frame and its header.
// ─────────────────────────────────────────────────────────────────────────────

export const JourneyYoidCard: React.FC<{
  saved: boolean;
  yoid: string;
  listingHref: string;
}> = ({ saved, yoid, listingHref }) => (
  <YoidThread
    bare
    steps={ziqiThreadSteps({ saved, complete: false })}
    yoid={yoid}
    className="grow"
  >
    <div className="mt-auto flex flex-col gap-0.5 text-xs md:text-sm">
      <span className="text-gray-dark">{journeyItem(1).title}</span>
      <Link
        href={listingHref}
        className="text-green inline-flex items-center gap-1 font-semibold hover:underline"
      >
        {saved ? "Open your saved job" : "Open the job to save it"}
        <IoArrowForward aria-hidden="true" className="h-4 w-4" />
      </Link>
    </div>
  </YoidThread>
);

export default JourneyYoidCard;
