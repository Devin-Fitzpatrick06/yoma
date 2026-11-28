// ─────────────────────────────────────────────────────────────────────────────
// The connected four-prototype journey (Ziqi → PathFinder → SkillLens → YoCV → back to the
// listing), Ziqi's side: the URL parameters, the link on to PathFinder, the handoff card copy and
// the YoID thread states. The rules and the wording are those of
// `docs/work/active/course-quality-judger/journey-contract.md` (v1); the wording is exact.
//
// Pure functions only (no `env`, no `window`), so the unit tests run without the app. The
// components read the build-time settings (`~/components/Journey/journeyConfig`) and pass them in.
// ─────────────────────────────────────────────────────────────────────────────

/** `journey=demo` on every journey link. */
export const JOURNEY_DEMO = "demo";

/** The shared mock-data id of the solar job: the listing the journey starts and ends on. */
export const JOURNEY_LISTING_SHARED_ID = "opp-job-001";

/** Ziqi's goal parameter on the link to PathFinder. */
export const JOURNEY_GOAL = "job";

/** The application id YoCV puts on the link back to the listing. */
export const JOURNEY_APPLICATION_ID = "app-001";

export const DEFAULT_JOURNEY_YOID = "yoid-ng-7f3a0123";
export const DEFAULT_PATHFINDER_URL = "http://localhost:4173";

export type JourneyAppKey = "ziqi" | "pathfinder" | "skilllens" | "yocv";
export type JourneyPhase = 1 | 2 | 3 | 4;

export interface JourneyItem {
  phase: JourneyPhase;
  /** The app's display name. */
  app: string;
  key: JourneyAppKey;
  /** The handoff item's name (the thread step). */
  name: string;
  /** The shared mock-data id. */
  id: string;
  /** The item's title. */
  title: string;
}

/** The contract's "Phases and handoff items" table, in phase order. */
export const JOURNEY_ITEMS: readonly JourneyItem[] = [
  {
    phase: 1,
    app: "Ziqi",
    key: "ziqi",
    name: "Saved Opportunity",
    id: "opp-job-001",
    title: "Solar Installation Assistant (Trainee)",
  },
  {
    phase: 2,
    app: "PathFinder",
    key: "pathfinder",
    name: "Learning Plan",
    id: "plan-001",
    title: "Plan step: complete a P2E course (Solar PV Installation Basics)",
  },
  {
    phase: 3,
    app: "SkillLens",
    key: "skilllens",
    name: "Skill Credential",
    id: "cred-002",
    title: "Solar PV Installation Basics: practical challenge passed",
  },
  {
    phase: 4,
    app: "YoCV",
    key: "yocv",
    name: "Job Application",
    id: "app-001",
    title: "Application to Solar Installation Assistant (Trainee)",
  },
];

export const journeyItem = (phase: JourneyPhase): JourneyItem =>
  JOURNEY_ITEMS[phase - 1]!;

export const RECORDED_LINE = "✓ Recorded on your YoID (mock-up)";
export const JOURNEY_COMPLETE_LINE =
  "✓ Journey complete: all four items are on your YoID (mock-up)";
export const SAVE_HINT = "Save this opportunity to hand it on to PathFinder.";

/** The thread's label: `YoID · yoid-ng-7f3a0123 · mock-up`. */
export const threadLabel = (yoid: string): string => `YoID · ${yoid} · mock-up`;

// ── Settings ─────────────────────────────────────────────────────────────────

export interface JourneyConfig {
  enabled: boolean;
  /** The Yoma opportunity id of `opp-job-001`; null when not set. */
  listingId: string | null;
  yoid: string;
  pathfinderUrl: string;
}

const trimmed = (value: string | undefined | null): string | null => {
  const t = (value ?? "").trim();
  return t.length > 0 ? t : null;
};

/** The build-time settings, with the contract's defaults for the optional ones. */
export const toJourneyConfig = (raw: {
  enabled: boolean;
  listingId?: string | null;
  yoid?: string | null;
  pathfinderUrl?: string | null;
}): JourneyConfig => ({
  enabled: raw.enabled === true,
  listingId: trimmed(raw.listingId),
  yoid: trimmed(raw.yoid) ?? DEFAULT_JOURNEY_YOID,
  pathfinderUrl: trimmed(raw.pathfinderUrl) ?? DEFAULT_PATHFINDER_URL,
});

/** True only with the journey on and `opportunityId` the configured journey listing. */
export const isJourneyListing = (
  config: JourneyConfig,
  opportunityId: string | null | undefined,
): boolean =>
  config.enabled &&
  !!config.listingId &&
  !!opportunityId &&
  config.listingId.toLowerCase() === opportunityId.trim().toLowerCase();

// ── URLs ─────────────────────────────────────────────────────────────────────

/** An absolute `http:` or `https:` URL; anything else (relative, `javascript:`, junk) is not. */
export const isHttpUrl = (value: string | null | undefined): boolean => {
  if (!value) return false;
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
};

/** The absolute URL of a Yoma listing, from the page's origin; null for a non-http(s) origin. */
export const listingUrl = (
  origin: string | null | undefined,
  opportunityId: string,
): string | null => {
  if (!origin || !isHttpUrl(origin) || !opportunityId) return null;
  return `${new URL(origin).origin}/opportunities/${encodeURIComponent(opportunityId)}`;
};

/**
 * Contract link 1, Ziqi → PathFinder:
 * `PATHFINDER_URL/?journey=demo&from=ziqi&yoid=…&listing=opp-job-001&goal=job&return=…`.
 * Null when PATHFINDER_URL is not http(s). A `returnUrl` that is not http(s) is left out.
 */
export const pathFinderLink = (input: {
  pathfinderUrl: string;
  yoid: string;
  returnUrl: string | null;
}): string | null => {
  if (!isHttpUrl(input.pathfinderUrl)) return null;
  const url = new URL(`${input.pathfinderUrl.replace(/\/+$/, "")}/`);
  url.search = "";
  url.hash = "";
  const params = new URLSearchParams();
  params.set("journey", JOURNEY_DEMO);
  params.set("from", "ziqi");
  params.set("yoid", input.yoid);
  params.set("listing", JOURNEY_LISTING_SHARED_ID);
  params.set("goal", JOURNEY_GOAL);
  if (isHttpUrl(input.returnUrl)) params.set("return", input.returnUrl!);
  url.search = params.toString();
  return url.href;
};

// ── Parameters ───────────────────────────────────────────────────────────────

export type QueryLike = Record<string, string | string[] | undefined>;

export interface JourneyParams {
  /** `journey=demo` */
  journey: boolean;
  from: string | null;
  yoid: string | null;
  listing: string | null;
  goal: string | null;
  plan: string | null;
  course: string | null;
  credential: string | null;
  application: string | null;
  /** Only an http(s) URL; anything else is ignored (null). */
  returnUrl: string | null;
}

const first = (value: string | string[] | undefined): string | null => {
  const v = Array.isArray(value) ? value[0] : value;
  return trimmed(v);
};

/** The contract's URL parameters, from Next's `router.query`. */
export const parseJourneyParams = (query: QueryLike): JourneyParams => {
  const returnUrl = first(query.return);
  return {
    journey: first(query.journey) === JOURNEY_DEMO,
    from: first(query.from),
    yoid: first(query.yoid),
    listing: first(query.listing),
    goal: first(query.goal),
    plan: first(query.plan),
    course: first(query.course),
    credential: first(query.credential),
    application: first(query.application),
    returnUrl: isHttpUrl(returnUrl) ? returnUrl : null,
  };
};

/** The journey's last screen: the listing opened by YoCV with `from=yocv&application=app-001`. */
export const isJourneyComplete = (params: JourneyParams): boolean =>
  params.journey &&
  params.from === "yocv" &&
  params.application === JOURNEY_APPLICATION_ID;

// ── The YoID thread ──────────────────────────────────────────────────────────

export type ThreadState = "recorded" | "in progress" | "next";

export interface ThreadStep {
  phase: JourneyPhase;
  name: string;
  state: ThreadState;
}

/**
 * The contract's thread table, for the app at `phase`: steps before it `recorded`, its own step
 * `in progress` until `done`, then `recorded`; steps after it `next`. `complete` (Ziqi's last
 * screen) records all four.
 */
export const threadSteps = (input: {
  phase: JourneyPhase;
  done: boolean;
  complete?: boolean;
}): ThreadStep[] =>
  JOURNEY_ITEMS.map((item) => ({
    phase: item.phase,
    name: item.name,
    state:
      input.complete || item.phase < input.phase
        ? "recorded"
        : item.phase === input.phase
          ? input.done
            ? "recorded"
            : "in progress"
          : "next",
  }));

/** Ziqi's thread: phase 1, done once the listing is saved; all four on the last screen. */
export const ziqiThreadSteps = (input: {
  saved: boolean;
  complete: boolean;
}): ThreadStep[] =>
  threadSteps({ phase: 1, done: input.saved, complete: input.complete });

// ── The handoff card ─────────────────────────────────────────────────────────

export type HandoffVariant = "arriving" | "leaving" | "final";

export interface HandoffCopy {
  variant: HandoffVariant;
  eyebrow: string;
  /** The item name, in bold. */
  title: string;
  itemTitle: string;
  /** The primary button's label (leaving only). */
  button: string | null;
  line: string;
}

/** Arriving at `phase` (2–4): `From {Previous app} · Phase {n-1}` and the previous item. */
export const arrivingCopy = (phase: 2 | 3 | 4): HandoffCopy => {
  const previous = journeyItem((phase - 1) as JourneyPhase);
  return {
    variant: "arriving",
    eyebrow: `From ${previous.app} · Phase ${previous.phase}`,
    title: previous.name,
    itemTitle: previous.title,
    button: null,
    line: RECORDED_LINE,
  };
};

/** Leaving `phase` (1–3): `Handing on to {Next app} · Phase {n+1}` and this phase's item. */
export const leavingCopy = (phase: 1 | 2 | 3): HandoffCopy => {
  const item = journeyItem(phase);
  const next = journeyItem((phase + 1) as JourneyPhase);
  return {
    variant: "leaving",
    eyebrow: `Handing on to ${next.app} · Phase ${next.phase}`,
    title: item.name,
    itemTitle: item.title,
    button: `Continue to ${next.app}`,
    line: RECORDED_LINE,
  };
};

/** Ziqi's last screen, the listing opened with `from=yocv`. */
export const finalCopy = (): HandoffCopy => {
  const application = journeyItem(4);
  return {
    variant: "final",
    eyebrow: `From ${application.app} · Phase ${application.phase}`,
    title: `${application.name} received`,
    itemTitle: application.title,
    button: null,
    line: JOURNEY_COMPLETE_LINE,
  };
};
