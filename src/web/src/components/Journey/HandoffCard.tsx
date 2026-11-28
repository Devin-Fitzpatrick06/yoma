import { useId } from "react";
import { IoArrowForward } from "react-icons/io5";
import type { HandoffCopy } from "~/lib/journey";

// ─────────────────────────────────────────────────────────────────────────────
// The journey's handoff card (contract v1, "The handoff card"): an eyebrow, the item name in
// bold, the item's title, a green "recorded" line and, leaving, a blue button with purple text
// (the same in all four apps; white on this blue is under 3:1, and the hover is the lighter
// blue-shade because purple on blue-dark is under 4.5:1) that is a real link in the same
// tab. The copy comes from `~/lib/journey` (arrivingCopy / leavingCopy / finalCopy), so the
// wording stays exact. Yoma tokens: white card, purple heading, green "recorded", blue button,
// Nunito heading, 12px radius.
// ─────────────────────────────────────────────────────────────────────────────

type HeadingLevel = 2 | 3 | 4;

const HEADINGS = { 2: "h2", 3: "h3", 4: "h4" } as const;

export const HandoffCard: React.FC<{
  copy: HandoffCopy;
  /** The button's target (leaving only). No button without one. */
  href?: string | null;
  headingLevel?: HeadingLevel;
  className?: string;
}> = ({ copy, href, headingLevel = 2, className = "" }) => {
  const headingId = useId();
  const Heading = HEADINGS[headingLevel];
  const showButton = copy.variant === "leaving" && !!copy.button && !!href;
  const accent =
    copy.variant === "final"
      ? "border-green"
      : copy.variant === "leaving"
        ? "border-blue"
        : "border-purple";

  return (
    <section
      aria-labelledby={headingId}
      data-journey-card={copy.variant}
      className={`flex w-full min-w-0 flex-col gap-1.5 rounded-xl border-l-4 bg-white p-4 shadow-lg md:p-5 ${accent} ${className}`}
    >
      {/* sentence case, as the contract writes it: no uppercase transform */}
      <p className="text-gray-dark text-xs font-semibold">{copy.eyebrow}</p>
      <Heading
        id={headingId}
        className="font-nunito text-purple text-lg font-black break-words md:text-xl"
      >
        {copy.title}
      </Heading>
      <p className="text-sm break-words text-black md:text-base">
        {copy.itemTitle}
      </p>

      <div className="mt-2 flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
        <p className="text-green text-sm font-semibold">{copy.line}</p>
        {showButton && (
          <a
            href={href!}
            className="btn bg-blue hover:bg-blue-shade text-purple h-11 min-h-11 rounded-full border-none px-6 normal-case max-sm:w-full"
          >
            {copy.button}
            <IoArrowForward aria-hidden="true" className="h-4 w-4" />
          </a>
        )}
      </div>
    </section>
  );
};

export default HandoffCard;
