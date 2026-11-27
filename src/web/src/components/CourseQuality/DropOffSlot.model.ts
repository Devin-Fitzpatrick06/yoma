import type { StepDropOff } from "~/api/models/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// Drop-off (W5): what a slot shows. No drop-off data exists yet, so every surface shows the
// Collecting state; a number appears only once the API says Available and sends a rate.
// ─────────────────────────────────────────────────────────────────────────────

export const DROP_OFF_COLLECTING_NOTE = "Collecting from the pilot";

export const DROP_OFF_COLLECTING_DETAIL =
  "Drop-off appears here once enough youth have taken each step.";

/** The Collecting state, built client-side where the API does not send drop-off yet. */
export const collectingDropOff = (): StepDropOff => ({
  status: "Collecting",
  rate: null,
  note: DROP_OFF_COLLECTING_NOTE,
});

export type DropOffView =
  | { kind: "collecting"; note: string }
  | { kind: "available"; percent: number; text: string; note: string };

const isUsableRate = (rate: number | null | undefined): rate is number =>
  typeof rate === "number" && Number.isFinite(rate);

const clampShare = (rate: number) => Math.max(0, Math.min(1, rate));

/** `rate` is a share from 0 to 1; shown as a whole percent, clamped to 0–100. */
export const formatDropOffRate = (rate: number): string =>
  `${Math.round(clampShare(rate) * 100)}%`;

/**
 * Never a number while Collecting, even if a rate came along; Available without a usable rate
 * also falls back to Collecting rather than show something invented.
 */
export const toDropOffView = (
  dropOff: StepDropOff | null | undefined,
): DropOffView => {
  const note = dropOff?.note?.trim() || DROP_OFF_COLLECTING_NOTE;
  if (dropOff?.status === "Available" && isUsableRate(dropOff.rate)) {
    return {
      kind: "available",
      percent: Math.round(clampShare(dropOff.rate) * 100),
      text: formatDropOffRate(dropOff.rate),
      note,
    };
  }
  return {
    kind: "collecting",
    note: dropOff?.status === "Collecting" ? note : DROP_OFF_COLLECTING_NOTE,
  };
};
