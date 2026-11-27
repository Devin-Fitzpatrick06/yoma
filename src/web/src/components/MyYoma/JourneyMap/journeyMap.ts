import type {
  MyYoma,
  PartnerBadgeLevel,
  PathwayCourse,
  PathwayNextStep,
  PathwayStage,
  PathwayStepType,
} from "~/api/models/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// The journey map's pure logic: the order of the stations, "You are here", which steps and stages
// were just finished (for the celebration) and the shape of the path line. Unit tested.
// ─────────────────────────────────────────────────────────────────────────────

/** A step on the map, in path order. `number` is 1-based across the whole path. */
export interface JourneyStep {
  course: PathwayCourse;
  number: number;
  stageIndex: number;
}

/** Steps in the order the youth takes them: stages by `order`, courses as the API sent them. */
export const orderedStages = (myYoma: Pick<MyYoma, "stages">): PathwayStage[] =>
  [...myYoma.stages].sort((a, b) => a.order - b.order);

export const journeySteps = (myYoma: Pick<MyYoma, "stages">): JourneyStep[] => {
  let number = 0;
  return orderedStages(myYoma).flatMap((stage, stageIndex) =>
    stage.courses.map((course) => {
      number += 1;
      return { course, number, stageIndex };
    }),
  );
};

/** Index into `journeySteps` of the first unfinished step; `steps.length` when all are done. */
export const youAreHereIndex = (
  steps: Pick<JourneyStep, "course">[],
): number => {
  const index = steps.findIndex((s) => s.course.status !== "Completed");
  return index === -1 ? steps.length : index;
};

/** A stage is complete when it has steps and every one is completed. */
export const isStageComplete = (
  stage: Pick<PathwayStage, "courses">,
): boolean =>
  stage.courses.length > 0 &&
  stage.courses.every((c) => c.status === "Completed");

export interface Celebrations {
  steps: string[]; // opportunity ids that became Completed
  stages: number[]; // stage `order`s that became complete
}

/**
 * What finished between two renders of the map. A step counts only when it was on the previous
 * path and not completed there (a swap or a first render never celebrates).
 */
export const celebrationsBetween = (
  previous: Pick<MyYoma, "stages"> | null,
  next: Pick<MyYoma, "stages">,
): Celebrations => {
  if (!previous) return { steps: [], stages: [] };
  const before = new Map(
    previous.stages.flatMap((s) =>
      s.courses.map((c) => [c.opportunityId, c.status] as const),
    ),
  );
  const steps = next.stages.flatMap((s) =>
    s.courses
      .filter((c) => {
        const was = before.get(c.opportunityId);
        return (
          c.status === "Completed" && was !== undefined && was !== "Completed"
        );
      })
      .map((c) => c.opportunityId),
  );
  const wasComplete = new Map(
    previous.stages.map((s) => [s.order, isStageComplete(s)] as const),
  );
  const stages = next.stages
    .filter((s) => isStageComplete(s) && wasComplete.get(s.order) === false)
    .map((s) => s.order);
  return { steps, stages };
};

export type StageKind = "Learn" | "Practise" | "Prove";

/**
 * The kicker over a stage: practise when it holds an impact action or task, prove for the last
 * stage of a longer path, learn otherwise.
 */
export const stageKind = (
  stage: Pick<PathwayStage, "courses">,
  index: number,
  total: number,
): StageKind => {
  if (stage.courses.some((c) => c.type !== "Learning")) return "Practise";
  if (total > 2 && index === total - 1) return "Prove";
  return "Learn";
};

export const STEP_TYPE_LABELS: Record<PathwayStepType, string> = {
  Learning: "Course",
  ImpactAction: "Impact action",
  Task: "Task",
};

/** Only earned levels show on a card; "no badge yet" is not worth the space there. */
export const visiblePartnerBadge = (
  level: PartnerBadgeLevel | null,
): Exclude<PartnerBadgeLevel, "None" | "NotEnoughGraded"> | null =>
  level === "Platinum" ||
  level === "Gold" ||
  level === "Silver" ||
  level === "Bronze"
    ? level
    : null;

/** "Young people who completed this usually go on to …": the most common next step first. */
export const topNextSteps = (
  next: PathwayNextStep[],
  limit = 2,
): PathwayNextStep[] =>
  [...next]
    .filter((n) => n.count > 0)
    .sort((a, b) => b.share - a.share || b.count - a.count)
    .slice(0, limit);

/** The hours line on a card. */
export const hoursLabel = (hours: number | null): string =>
  hours === null ? "Length not listed" : `${hours} h`;

/**
 * The reserved drop-off slot's text: the API's note, labelled "Drop-off" unless the note already
 * says so. Never a rate: the slot shows only that collection is under way.
 */
export const dropOffLine = (note: string | null | undefined): string => {
  const text = note?.trim() || "Collecting from the pilot";
  return /^drop-?off\b/i.test(text)
    ? text
    : `Drop-off: ${text.charAt(0).toLowerCase()}${text.slice(1)}`;
};

// ── The path line ───────────────────────────────────────────────────────────

export interface Point {
  x: number;
  y: number;
}

/**
 * An SVG path through every station centre: a gentle quadratic curve between neighbours that
 * bows to alternate sides, so the trail meanders but always passes through each dot.
 */
export const trailPath = (points: Point[], bow = 18): string => {
  if (points.length === 0) return "";
  const [first, ...rest] = points;
  let d = `M ${round(first!.x)} ${round(first!.y)}`;
  let previous = first!;
  rest.forEach((p, i) => {
    const c = control(previous, p, i, bow);
    d += ` Q ${round(c.x)} ${round(c.y)} ${round(p.x)} ${round(p.y)}`;
    previous = p;
  });
  return d;
};

/** The control point `trailPath` uses between two points, for the length estimate. */
const control = (a: Point, b: Point, i: number, bow: number): Point => {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const length = Math.hypot(dx, dy) || 1;
  const offset = Math.min(bow, length * 0.18) * (i % 2 === 0 ? 1 : -1);
  return {
    x: (a.x + b.x) / 2 + (-dy / length) * offset,
    y: (a.y + b.y) / 2 + (dx / length) * offset,
  };
};

const quadLength = (a: Point, c: Point, b: Point, samples = 16): number => {
  let length = 0;
  let last = a;
  for (let s = 1; s <= samples; s += 1) {
    const t = s / samples;
    const u = 1 - t;
    const p = {
      x: u * u * a.x + 2 * u * t * c.x + t * t * b.x,
      y: u * u * a.y + 2 * u * t * c.y + t * t * b.y,
    };
    length += Math.hypot(p.x - last.x, p.y - last.y);
    last = p;
  }
  return length;
};

/**
 * How far along the trail (0–1) the station at `index` sits, so the "done" line can stop at
 * "You are here". Clamped; 0 for fewer than two points.
 */
export const trailFraction = (
  points: Point[],
  index: number,
  bow = 18,
): number => {
  if (points.length < 2) return 0;
  const segments = points
    .slice(1)
    .map((p, i) => quadLength(points[i]!, control(points[i]!, p, i, bow), p));
  const total = segments.reduce((a, b) => a + b, 0);
  if (total === 0) return 0;
  const upTo = Math.max(0, Math.min(index, segments.length));
  const done = segments.slice(0, upTo).reduce((a, b) => a + b, 0);
  return done / total;
};

const round = (n: number): number => Math.round(n * 10) / 10;
