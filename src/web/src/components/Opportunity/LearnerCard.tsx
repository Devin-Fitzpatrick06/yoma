import moment from "moment";
import { useId } from "react";
import {
  IoBookOutline,
  IoCheckmarkCircle,
  IoChevronDown,
  IoEllipse,
  IoHourglassOutline,
  IoShieldCheckmark,
  IoTimeOutline,
  IoVideocamOutline,
  IoWalletOutline,
} from "react-icons/io5";
import type {
  CourseFormat,
  LearnerCard as LearnerCardData,
  LearnerCardCost,
} from "~/api/models/courseQuality";
import { DATE_FORMAT_HUMAN } from "~/lib/constants";

// ─────────────────────────────────────────────────────────────────────────────
// What a learner reads before starting a course: is it verified, how long does it take, when
// does it run, and what does it cost up front. It sits in the opportunity header, above the
// start action. Tokens are the header card's (Nunito black values, green and grey panels); it
// has no images, web fonts beyond the app's or motion, so it stays light on a low-end phone.
//
// The rail between the start and end dates is the card's one graphic: solid when both dates are
// known, dashed towards a hollow end point when there is no end date.
// ─────────────────────────────────────────────────────────────────────────────

const MAX_HIGHLIGHTS = 6;

// Unknown has no entry: the card shows no format rather than guessing one
const FORMAT_LABELS: Partial<
  Record<CourseFormat, { label: string; Icon: React.ElementType }>
> = {
  Course: { label: "Course", Icon: IoBookOutline },
  Webinar: { label: "Webinar", Icon: IoVideocamOutline },
};

const formatDate = (date: string): string =>
  moment.utc(date).format(DATE_FORMAT_HUMAN);

const hasText = (value: string | null | undefined): value is string =>
  !!value?.trim();

/** Free or Paid need evidence, so anything without it reads as unconfirmed, never as "Free". */
const getCostDisplay = (
  cost: LearnerCardCost,
): { value: string; detail: string | null } => {
  const unconfirmed = {
    value: "Cost not confirmed — check before you enrol",
    detail: null,
  };

  if (!hasText(cost.evidence)) return unconfirmed;

  if (cost.type === "Free") return { value: "Free", detail: null };

  if (cost.type === "Paid") {
    return cost.amount != null && cost.amount > 0 && hasText(cost.currency)
      ? {
          value: `${cost.currency} ${cost.amount.toLocaleString("en-GB")}`,
          detail: "Paid",
        }
      : { value: "Paid", detail: "Check the price before you enrol" };
  }

  return unconfirmed;
};

/** The summary only: commitmentHours counts a week as 168 hours, so it is not learning time. */
const getTimeDisplay = (
  timelineSummary: string | null,
): { value: string; detail: string | null } =>
  hasText(timelineSummary)
    ? { value: timelineSummary.trim(), detail: null }
    : { value: "Time not confirmed", detail: "Check before you enrol" };

const FactLabel: React.FC<{
  icon: React.ReactNode;
  children: React.ReactNode;
}> = ({ icon, children }) => (
  <dt className="text-gray-dark flex items-center gap-1.5 text-xs font-semibold">
    {icon}
    {children}
  </dt>
);

const FACT_VALUE =
  "font-nunito text-base leading-snug font-black wrap-break-word text-black md:text-xl";
const FACT_DETAIL = "text-gray-dark text-sm wrap-break-word md:mt-0.5";

const LearnerCard: React.FC<{ card: LearnerCardData }> = ({ card }) => {
  const headingId = useId();
  // Anything other than Verified reads as "Not yet verified", so a new status cannot overclaim
  const verified = card.status === "Verified";
  const time = getTimeDisplay(card.timelineSummary);
  // Suspect dates can reach the card, so its dates only show once a page has confirmed them
  const datesChecked = card.datesConfirmed;
  const cost = getCostDisplay(card.cost);
  const highlights = card.highlights.filter(hasText).slice(0, MAX_HIGHLIGHTS);
  const checkedDate = formatDate(card.dateAssessed);

  const rail = verified ? "bg-green" : "bg-gray-dark";
  const railBorder = verified ? "border-green" : "border-gray-dark";
  const format = FORMAT_LABELS[card.format];
  // never on a listing that has an end date
  const startAnytime = card.selfPaced && !card.dateEnd;
  const statusNote = verified
    ? startAnytime
      ? "This listing is live and matches its course page. It is self-paced, so you can start anytime."
      : datesChecked
        ? "This listing is live, and its dates and link have been checked."
        : "This listing has been checked. Confirm the dates on the course page before you start."
    : "Yoma has not confirmed these details yet. Check them on the course page before you start.";
  const highlightList = highlights.length > 0 && (
    <ul className="grid gap-1.5 md:mt-1.5 md:grid-cols-2 md:gap-x-8">
      {highlights.map((highlight, index) => (
        <li
          key={`${index}-${highlight}`}
          className="flex items-start gap-2 text-sm leading-snug text-black"
        >
          {/* a tick says "passed", which only a verified card can claim */}
          {verified ? (
            <IoCheckmarkCircle
              aria-hidden="true"
              className="text-green mt-px h-4 w-4 shrink-0"
            />
          ) : (
            <IoEllipse
              aria-hidden="true"
              className="text-gray-dark mx-[5px] mt-1.5 h-1.5 w-1.5 shrink-0"
            />
          )}
          <span className="min-w-0 wrap-break-word">{highlight}</span>
        </li>
      ))}
    </ul>
  );

  return (
    <section
      aria-labelledby={headingId}
      className="border-gray mt-4 overflow-hidden rounded-2xl border bg-white md:mt-5"
    >
      {/* STATUS: the words and the icon carry the state, the colour only supports them */}
      <div
        className={`flex items-center gap-2.5 px-3 py-2 md:items-start md:gap-3 md:px-5 md:py-3 ${
          verified ? "bg-green text-white" : "bg-gray-light text-black"
        }`}
      >
        {verified ? (
          <IoShieldCheckmark
            aria-hidden="true"
            className="h-6 w-6 shrink-0 md:mt-0.5"
          />
        ) : (
          <IoHourglassOutline
            aria-hidden="true"
            className="text-gray-dark h-6 w-6 shrink-0 md:mt-0.5"
          />
        )}
        <div className="min-w-0 grow">
          <h5
            id={headingId}
            className="font-nunito text-lg leading-snug font-black"
          >
            {verified ? "Verified by Yoma" : "Not yet verified"}
          </h5>
          <p className="hidden text-sm leading-snug md:block">{statusNote}</p>
        </div>
        {format && (
          <span className="text-purple inline-flex shrink-0 items-center gap-1 rounded-full bg-white px-2.5 py-1 text-sm font-bold md:mt-0.5">
            <format.Icon aria-hidden="true" className="h-4 w-4" />
            <span className="sr-only">Format: </span>
            {format.label}
          </span>
        )}
      </div>

      <div className="flex flex-col gap-2.5 px-3 pt-2.5 pb-1 md:gap-4 md:p-5">
        <div className="grid gap-2.5 md:grid-cols-[minmax(0,3fr)_minmax(0,2fr)] md:gap-8">
          {/* TIMELINE */}
          <div>
            <dl>
              <FactLabel
                icon={<IoTimeOutline aria-hidden="true" className="h-4 w-4" />}
              >
                Time needed
              </FactLabel>
              <dd className={`${FACT_VALUE} md:mt-0.5`}>{time.value}</dd>
              {time.detail && <dd className={FACT_DETAIL}>{time.detail}</dd>}
            </dl>

            {startAnytime ? (
              <p className="mt-2 text-sm font-semibold text-black md:mt-3">
                Start anytime
              </p>
            ) : datesChecked ? (
              <>
                <div
                  aria-hidden="true"
                  className="mt-2 flex items-center px-px md:mt-3"
                >
                  <span
                    className={`h-2.5 w-2.5 shrink-0 rounded-full ${rail}`}
                  />
                  {card.dateEnd ? (
                    <>
                      <span className={`h-0.5 grow ${rail}`} />
                      <span
                        className={`h-2.5 w-2.5 shrink-0 rounded-full ${rail}`}
                      />
                    </>
                  ) : (
                    <>
                      <span
                        className={`grow border-t-2 border-dashed ${railBorder}`}
                      />
                      <span
                        className={`h-2.5 w-2.5 shrink-0 rounded-full border-2 ${railBorder}`}
                      />
                    </>
                  )}
                </div>

                {/* below md each date shares a line with its label */}
                <dl className="mt-1 flex justify-between gap-3 md:mt-1.5">
                  <div className="flex min-w-0 items-baseline gap-1 md:block">
                    <dt className="text-gray-dark text-xs">Starts</dt>
                    <dd className="text-xs font-semibold text-black md:text-sm">
                      {formatDate(card.dateStart)}
                    </dd>
                  </div>
                  <div className="flex min-w-0 items-baseline gap-1 md:block md:text-right">
                    <dt className="text-gray-dark text-xs">Ends</dt>
                    <dd className="text-xs font-semibold text-black md:text-sm">
                      {card.dateEnd
                        ? formatDate(card.dateEnd)
                        : "No end date set"}
                    </dd>
                  </div>
                </dl>
              </>
            ) : (
              <p className="text-gray-dark mt-2 text-sm md:mt-3">
                Dates to be confirmed
              </p>
            )}
          </div>

          {/* UPFRONT COST: label and value share the row below md */}
          <dl className="border-gray grid grid-cols-[auto_minmax(0,1fr)] items-baseline gap-x-3 border-t pt-2.5 md:block md:border-t-0 md:border-l md:pt-0 md:pl-8">
            <FactLabel
              icon={<IoWalletOutline aria-hidden="true" className="h-4 w-4" />}
            >
              Upfront cost
            </FactLabel>
            <dd className={`${FACT_VALUE} text-right md:mt-0.5 md:text-left`}>
              {cost.value}
            </dd>
            {cost.detail && (
              <dd
                className={`${FACT_DETAIL} col-start-2 text-right md:text-left`}
              >
                {cost.detail}
              </dd>
            )}
          </dl>
        </div>

        {/* WHAT WAS CHECKED, AND WHEN. Below md it folds away, so the start action stays in view */}
        <details className="group border-gray border-t md:hidden">
          <summary className="flex min-h-11 cursor-pointer list-none items-center gap-2 [&::-webkit-details-marker]:hidden">
            <span className="grow text-sm font-bold text-black">
              {highlights.length > 0 ? "What we checked" : "About this check"}
            </span>
            <span className="text-gray-dark text-xs">
              Checked {checkedDate}
            </span>
            <IoChevronDown
              aria-hidden="true"
              className="text-gray-dark h-4 w-4 shrink-0 group-open:rotate-180"
            />
          </summary>
          <div className="flex flex-col gap-2 pb-3">
            <p className="text-gray-dark text-sm leading-snug">{statusNote}</p>
            {highlightList}
          </div>
        </details>
        <div className="border-gray hidden border-t pt-3 md:block">
          <div className="flex flex-wrap items-baseline justify-between gap-x-4">
            {highlights.length > 0 && (
              <h6 className="text-sm font-bold text-black">What we checked</h6>
            )}
            <p className="text-gray-dark text-xs">Checked {checkedDate}</p>
          </div>
          {highlightList}
        </div>
      </div>
    </section>
  );
};

export default LearnerCard;
