import type { PathwayHealth } from "~/api/models/pathway";
import { BTN_SECONDARY } from "~/components/Common/buttonStyles";
import DropOffSlot from "./DropOffSlot";
import {
  formatCount,
  pathwayHealthExtraNote,
} from "./PathwayHealthPanel.model";

// ─────────────────────────────────────────────────────────────────────────────
// Admin "Pathway health" (W5), shown on the Ziqi reviewer queue when NEXT_PUBLIC_PATHWAY_ENABLED
// is on: steps tracked, events recorded, and the drop-off block in its Collecting state.
// ─────────────────────────────────────────────────────────────────────────────

const Count: React.FC<{ label: string; value: string }> = ({
  label,
  value,
}) => (
  <div className="bg-gray-light flex flex-col rounded-lg px-3 py-2">
    <dt className="text-gray-dark text-xs">{label}</dt>
    <dd className="font-nunito text-lg font-black text-black">{value}</dd>
  </div>
);

const PathwayHealthPanel: React.FC<{
  health: PathwayHealth | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry?: () => void;
}> = ({ health, isLoading, isError, onRetry }) => {
  const extraNote = health ? pathwayHealthExtraNote(health) : null;

  return (
    <section
      aria-labelledby="pathway-health-heading"
      className="flex flex-col gap-3 rounded-lg bg-white p-4 shadow-sm md:p-5"
    >
      <div className="flex flex-col gap-0.5">
        <h2
          id="pathway-health-heading"
          className="font-nunito text-lg font-black text-black"
        >
          Pathway health
        </h2>
        <p className="text-gray-dark text-sm">
          How youth move through their My Yoma paths. Counts only; no youth is
          named.
        </p>
      </div>

      {isError && !health ? (
        <div role="alert" className="flex items-center gap-3 text-sm">
          Pathway health could not be loaded.
          {onRetry && (
            <button type="button" className={BTN_SECONDARY} onClick={onRetry}>
              Try again
            </button>
          )}
        </div>
      ) : isLoading || !health ? (
        <p className="text-gray-dark text-sm">Loading pathway health...</p>
      ) : (
        <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
          <dl className="grid grid-cols-2 gap-2">
            <Count
              label="Steps tracked"
              value={formatCount(health.stepsTracked)}
            />
            <Count label="Events" value={formatCount(health.events)} />
          </dl>
          <div className="flex flex-col gap-2">
            <DropOffSlot dropOff={health.dropOff} showDetail={true} />
            {extraNote && (
              <p className="text-gray-dark text-xs break-words">{extraNote}</p>
            )}
          </div>
        </div>
      )}
    </section>
  );
};

export default PathwayHealthPanel;
