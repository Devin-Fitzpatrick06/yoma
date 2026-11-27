import type { MyPathSummary } from "~/api/models/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// The YoID "My path" card (W3 write-back): pure helpers for its progress ring and stage bars.
// ─────────────────────────────────────────────────────────────────────────────

export const MY_PATH_HREF = "/my-yoma";

export const opportunityHref = (opportunityId: string) =>
  `/opportunities/${encodeURIComponent(opportunityId)}`;

/** Whole percent of `done` out of `total`, clamped to 0–100; 0 when there is nothing to do. */
export const percentOf = (done: number, total: number): number => {
  if (!Number.isFinite(done) || !Number.isFinite(total) || total <= 0) return 0;
  return Math.round(Math.max(0, Math.min(1, done / total)) * 100);
};

const clampCount = (done: number, total: number) =>
  Math.max(0, Math.min(total, Number.isFinite(done) ? done : 0));

export interface PathProgress {
  completed: number;
  total: number;
  percent: number;
  label: string; // e.g. "3 of 11 steps done"
}

export const toPathProgress = (summary: MyPathSummary): PathProgress => {
  const total = Math.max(0, Math.floor(summary.totalSteps || 0));
  const completed = clampCount(summary.completedSteps, total);
  return {
    completed,
    total,
    percent: percentOf(completed, total),
    label: `${completed} of ${total} ${total === 1 ? "step" : "steps"} done`,
  };
};

export interface StageProgress {
  name: string;
  completed: number;
  total: number;
  percent: number;
  done: boolean;
  label: string; // for screen readers, e.g. "Foundations: 2 of 3 done"
}

export const toStageProgress = (
  stages: MyPathSummary["stages"] | null | undefined,
): StageProgress[] =>
  (stages ?? []).map((stage) => {
    const total = Math.max(0, Math.floor(stage.total || 0));
    const completed = clampCount(stage.completed, total);
    return {
      name: stage.name,
      completed,
      total,
      percent: percentOf(completed, total),
      done: total > 0 && completed >= total,
      label: `${stage.name}: ${completed} of ${total} done`,
    };
  });

/** SVG circle stroke values for a ring of radius `radius` filled to `percent`. */
export const ringStroke = (
  percent: number,
  radius: number,
): { circumference: number; offset: number } => {
  const circumference = 2 * Math.PI * radius;
  const clamped = Math.max(0, Math.min(100, percent));
  return { circumference, offset: circumference * (1 - clamped / 100) };
};
