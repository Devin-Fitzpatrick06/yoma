import type { PathwayHealth } from "~/api/models/pathway";
import { DROP_OFF_COLLECTING_NOTE } from "./DropOffSlot.model";

// ─────────────────────────────────────────────────────────────────────────────
// The admin "Pathway health" panel (W5): how much step data the pilot has collected so far.
// The drop-off block reads "Collecting from the pilot" and "Drop-off appears here once enough
// youth have taken each step." until the API reports drop-off as Available.
// ─────────────────────────────────────────────────────────────────────────────

const COUNT_FORMAT = new Intl.NumberFormat("en-GB");

/** A whole, non-negative count with thousands separators; "0" for anything unusable. */
export const formatCount = (value: number | null | undefined): string =>
  COUNT_FORMAT.format(
    typeof value === "number" && Number.isFinite(value)
      ? Math.max(0, Math.round(value))
      : 0,
  );

/** The API's own note, when it says something the drop-off block does not already say. */
export const pathwayHealthExtraNote = (
  health: PathwayHealth,
): string | null => {
  const note = health.note?.trim();
  if (!note) return null;
  const dropOffNote = health.dropOff?.note?.trim() || DROP_OFF_COLLECTING_NOTE;
  return note.toLowerCase() === dropOffNote.toLowerCase() ? null : note;
};
