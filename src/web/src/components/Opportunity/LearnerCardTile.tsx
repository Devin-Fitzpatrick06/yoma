import Link from "next/link";
import { useId, useRef, useState } from "react";
import {
  IoClose,
  IoHourglassOutline,
  IoShieldCheckmark,
  IoTimeOutline,
  IoWalletOutline,
} from "react-icons/io5";
import type { OpportunityInfo } from "~/api/models/opportunity";
import CustomModal from "../Common/CustomModal";
import LearnerCard, {
  COST_NOT_CONFIRMED,
  FORMAT_LABELS,
  formatDate,
  getCostDisplay,
  getTimeDisplay,
  isStartAnytime,
} from "./LearnerCard";
import {
  OpportunityOrgCountriesRow,
  OpportunityTitleRow,
} from "./opportunityTypeTheme";

// ─────────────────────────────────────────────────────────────────────────────
// A course while browsing: the learner card's facts, at the size of the listing cards around it,
// so a learner can compare status, time and cost while scrolling. Selecting it flips the full
// card out over the page. The search results carry the card, so neither needs a request.
// ─────────────────────────────────────────────────────────────────────────────

const LearnerCardPopup: React.FC<{
  data: OpportunityInfo;
  onClose: () => void;
}> = ({ data, onClose }) => {
  const titleId = useId();

  return (
    <CustomModal
      isOpen
      onRequestClose={onClose}
      shouldCloseOnOverlayClick
      animationStyle="flip"
      className="!m-auto h-fit max-h-[90dvh] w-[calc(100%-2rem)] max-w-[560px] !rounded-3xl"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="flex flex-col gap-1 p-4 md:p-5"
      >
        <div className="flex items-start gap-3">
          <div className="min-w-0 grow">
            <h2
              id={titleId}
              className="font-nunito text-lg leading-snug font-black text-black md:text-xl"
            >
              {data.title}
            </h2>
            <OpportunityOrgCountriesRow data={data} />
          </div>
          <button
            type="button"
            aria-label="Close"
            onClick={onClose}
            className="btn btn-circle btn-sm bg-gray-light hover:bg-gray shrink-0 border-none text-black"
          >
            <IoClose aria-hidden="true" className="h-5 w-5" />
          </button>
        </div>

        {data.learnerCard ? (
          <LearnerCard card={data.learnerCard} />
        ) : (
          <p className="bg-gray-light mt-4 rounded-2xl p-4 text-sm text-black">
            Yoma has not confirmed these details yet. Check them on the course
            page before you start.
          </p>
        )}

        <Link
          href={`/opportunities/${data.id}`}
          target="_blank"
          rel="noopener noreferrer"
          className="bg-green hover:bg-green-dark mt-4 flex h-10 w-full items-center justify-center rounded-lg text-sm font-semibold text-white duration-300"
        >
          View course details
        </Link>
      </div>
    </CustomModal>
  );
};

const LearnerCardTile: React.FC<{ data: OpportunityInfo }> = ({ data }) => {
  const [open, setOpen] = useState(false);
  const openButton = useRef<HTMLButtonElement>(null);

  const card = data.learnerCard ?? null;
  const verified = data.qualityStatus === "Verified";
  const time = card
    ? getTimeDisplay(card.timelineSummary)
    : getTimeDisplay(data.commitmentIntervalDescription);
  const cost = card ? getCostDisplay(card.cost) : null;
  const costValue =
    !cost || cost.value === COST_NOT_CONFIRMED ? "Not confirmed" : cost.value;
  const format = card ? FORMAT_LABELS[card.format] : undefined;

  const close = () => {
    setOpen(false);
    openButton.current?.focus();
  };

  return (
    <>
      <article className="relative flex h-94 w-80 flex-col overflow-hidden rounded-2xl bg-white max-[370px]:w-64">
        {/* STATUS: the words and the icon carry the state, the colour only supports them */}
        <div
          className={`flex h-14 shrink-0 items-center gap-2 px-3 ${
            verified ? "bg-green text-white" : "bg-gray-light text-black"
          }`}
        >
          {verified ? (
            <IoShieldCheckmark
              aria-hidden="true"
              className="h-6 w-6 shrink-0"
            />
          ) : (
            <IoHourglassOutline
              aria-hidden="true"
              className="text-gray-dark h-6 w-6 shrink-0"
            />
          )}
          <span className="font-nunito min-w-0 grow truncate text-base font-black">
            {verified ? "Verified by Yoma" : "Not yet verified"}
          </span>
          {format && (
            <span className="text-purple inline-flex shrink-0 items-center gap-1 rounded-full bg-white px-2 py-0.5 text-xs font-bold">
              <format.Icon aria-hidden="true" className="h-3.5 w-3.5" />
              {format.label}
            </span>
          )}
        </div>

        <div className="flex min-h-0 flex-1 flex-col gap-2 p-3">
          <OpportunityTitleRow data={data} lines={2} />
          <OpportunityOrgCountriesRow data={data} />

          <dl className="border-gray mt-1 grid grid-cols-2 gap-3 border-t pt-2.5">
            <div className="min-w-0">
              <dt className="text-gray-dark flex items-center gap-1 text-xs font-semibold">
                <IoTimeOutline aria-hidden="true" className="h-4 w-4" />
                Time needed
              </dt>
              <dd className="font-nunito truncate text-base leading-snug font-black text-black">
                {time.value}
              </dd>
            </div>
            <div className="border-gray min-w-0 border-l pl-3">
              <dt className="text-gray-dark flex items-center gap-1 text-xs font-semibold">
                <IoWalletOutline aria-hidden="true" className="h-4 w-4" />
                Upfront cost
              </dt>
              <dd className="font-nunito truncate text-base leading-snug font-black text-black">
                {costValue}
              </dd>
            </div>
          </dl>

          <p className="truncate text-sm font-semibold text-black">
            {card && isStartAnytime(card)
              ? "Start anytime"
              : card?.datesConfirmed
                ? `${formatDate(card.dateStart)} – ${card.dateEnd ? formatDate(card.dateEnd) : "No end date"}`
                : "Dates to be confirmed"}
          </p>

          <div className="mt-auto space-y-2">
            {card && (
              <p className="text-gray-dark text-xs">
                Checked {formatDate(card.dateAssessed)}
              </p>
            )}
            {/* stretched over the tile, so the whole card opens the pop-up from one control */}
            <button
              ref={openButton}
              type="button"
              aria-haspopup="dialog"
              onClick={() => setOpen(true)}
              className="bg-green hover:bg-green-dark flex h-10 w-full items-center justify-center rounded-lg text-sm font-semibold text-white duration-300 after:absolute after:inset-0"
            >
              See learner card
            </button>
          </div>
        </div>
      </article>

      {/* beside the tile, so clicks inside the portal cannot reach the tile's button */}
      {open && <LearnerCardPopup data={data} onClose={close} />}
    </>
  );
};

export default LearnerCardTile;
