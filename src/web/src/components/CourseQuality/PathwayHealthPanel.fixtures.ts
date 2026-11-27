import type { PathwayHealth } from "~/api/models/pathway";
import { DROP_OFF_COLLECTING_NOTE } from "./DropOffSlot.model";

// Example data for the fixture preview (`/ziqi-admin-preview`); not pilot data.

export const PATHWAY_HEALTH_COLLECTING_FIXTURE: PathwayHealth = {
  stepsTracked: 37,
  events: 1284,
  dropOff: { status: "Collecting", rate: null, note: DROP_OFF_COLLECTING_NOTE },
  note: DROP_OFF_COLLECTING_NOTE,
};

/** How the panel will look once the pilot has enough youth per step (example rate only). */
export const PATHWAY_HEALTH_AVAILABLE_FIXTURE: PathwayHealth = {
  stepsTracked: 112,
  events: 20417,
  dropOff: {
    status: "Available",
    rate: 0.18,
    note: "Across steps with at least 20 youth",
  },
  note: "Example only: no drop-off data exists yet.",
};
