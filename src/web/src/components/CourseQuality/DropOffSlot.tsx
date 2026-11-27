import { IoHourglassOutline, IoTrendingDown } from "react-icons/io5";
import type { StepDropOff } from "~/api/models/pathway";
import { DROP_OFF_COLLECTING_DETAIL, toDropOffView } from "./DropOffSlot.model";

// ─────────────────────────────────────────────────────────────────────────────
// The reserved drop-off slot (W5): shows "Collecting from the pilot" until the API reports the
// step's drop-off as Available with a rate; never a number before that.
// ─────────────────────────────────────────────────────────────────────────────

const DropOffSlot: React.FC<{
  dropOff: StepDropOff | null | undefined;
  /** Adds the one-line explanation under the Collecting note. */
  showDetail?: boolean;
  className?: string;
}> = ({ dropOff, showDetail = false, className = "" }) => {
  const view = toDropOffView(dropOff);

  return (
    <div
      role="group"
      aria-label="Drop-off"
      className={`bg-gray-light flex flex-col gap-1 rounded-lg px-3 py-2 ${className}`}
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-gray-dark text-xs font-semibold tracking-wide uppercase">
          Drop-off
        </span>
        {view.kind === "collecting" ? (
          <span className="text-gray-dark inline-flex items-center gap-1 text-sm">
            <IoHourglassOutline aria-hidden="true" className="h-4 w-4" />
            {view.note}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 text-sm">
            <IoTrendingDown
              aria-hidden="true"
              className="text-orange h-4 w-4"
            />
            <span className="font-nunito font-black">{view.text}</span>
            <span className="text-gray-dark">did not continue</span>
          </span>
        )}
      </div>
      {view.kind === "available" ? (
        <div
          role="meter"
          aria-label="Drop-off rate"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={view.percent}
          aria-valuetext={`${view.text} of youth who started this step did not continue`}
          className="h-1.5 w-full rounded-full bg-white"
        >
          <div
            className="bg-orange h-full rounded-full"
            style={{ width: `${view.percent}%` }}
          />
        </div>
      ) : (
        showDetail && (
          <p className="text-gray-dark text-xs">{DROP_OFF_COLLECTING_DETAIL}</p>
        )
      )}
    </div>
  );
};

export default DropOffSlot;
