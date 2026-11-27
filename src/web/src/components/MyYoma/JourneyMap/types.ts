import type {
  MyYoma,
  PathwayCourse,
  PathwayStepEventType,
} from "~/api/models/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// The journey map's interface (iteration 4, W7). The map is presentational: it gets the path and
// callbacks, and never fetches. `pages/my-yoma` owns data and wires these to `api/services/pathway`.
// Shared contract between the map engineer and the first-visit engineer; change only through the lead.
// ─────────────────────────────────────────────────────────────────────────────

export interface JourneyMapProps {
  myYoma: MyYoma;
  /** true on the first render after onboarding: the path draws itself and cards fly in and lock */
  animateEntrance: boolean;
  onLock: (opportunityId: string, locked: boolean) => Promise<void>;
  /** loads up to three alternatives for the swap sheet */
  loadAlternatives: (opportunityId: string) => Promise<PathwayCourse[]>;
  onSwap: (
    opportunityId: string,
    replacementOpportunityId: string,
  ) => Promise<void>;
  /** drop-off collection; fire and forget */
  onStepEvent: (opportunityId: string, event: PathwayStepEventType) => void;
  onChangeAnswers: () => void;
}
