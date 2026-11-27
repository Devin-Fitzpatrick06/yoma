import type {
  DraftCheckField,
  DraftCheckFix,
  DraftCheckResult,
  DraftCheckVerdict,
} from "~/api/models/courseQuality";
import type { OpportunityRequestBase } from "~/api/models/opportunity";

// ─────────────────────────────────────────────────────────────────────────────
// The "Ziqi check" panel on the partner listing form (iteration 4, W1), without React or the DOM so
// the unit tests (`pnpm test`) cover it: the verdict wording, where each fix lives on the form, which
// form values make up the draft, and the debounced, cancellable request.
// ─────────────────────────────────────────────────────────────────────────────

/** Ziqi's predicted verdict in partner wording: a prediction, so softer than the final status. */
export const VERDICT_LABELS: Record<DraftCheckVerdict, string> = {
  Verified: "Verified",
  NeedsReview: "Needs a look",
  NotVerified: "Not verified yet",
};

/** Yoma tokens; green marks verification only. */
export const VERDICT_STYLES: Record<
  DraftCheckVerdict,
  { badge: string; ring: string }
> = {
  Verified: { badge: "bg-green-light text-green", ring: "text-green" },
  NeedsReview: { badge: "bg-yellow-tint text-black", ring: "text-yellow" },
  NotVerified: { badge: "bg-gray-light text-gray-dark", ring: "text-purple" },
};

export const verdictLabel = (verdict: string | null | undefined): string =>
  VERDICT_LABELS[verdict as DraftCheckVerdict] ?? VERDICT_LABELS.NotVerified;

export const verdictStyle = (verdict: string | null | undefined) =>
  VERDICT_STYLES[verdict as DraftCheckVerdict] ?? VERDICT_STYLES.NotVerified;

/** A 0–100 score, clamped and rounded, so a bad value never breaks the ring. */
export const clampScore = (score: number | null | undefined): number =>
  typeof score === "number" && Number.isFinite(score)
    ? Math.round(Math.min(100, Math.max(0, score)))
    : 0;

/** The ring's stroke: the share of the circumference the score covers. */
export const scoreRingDash = (score: number, radius: number) => {
  const circumference = 2 * Math.PI * radius;
  const filled = (clampScore(score) / 100) * circumference;
  return {
    circumference,
    dashArray: `${filled} ${circumference}`,
  };
};

/** The fixes that keep a listing from being verified come first; otherwise the API's order. */
export const sortFixes = (fixes: DraftCheckFix[] | null | undefined) =>
  [...(fixes ?? [])]
    .map((fix, index) => ({ fix, index }))
    .sort(
      (a, b) =>
        Number(b.fix.hardGate) - Number(a.fix.hardGate) || a.index - b.index,
    )
    .map(({ fix }) => fix);

export const fixCountSummary = (result: DraftCheckResult): string => {
  const count = result.fixes?.length ?? 0;
  if (count === 0) return "No fixes";
  return `${count} ${count === 1 ? "fix" : "fixes"}`;
};

// ── Where each fix lives on the listing form ────────────────────────────────
// The editor is a wizard: General (1), Details (2), Rewards (3), Skills (4), … Each target names the
// step and the selectors that find the field once that step is shown, tried in order.

export interface DraftFieldTarget {
  step: number;
  stepLabel: string;
  label: string;
  selectors: string[];
}

export const DRAFT_FIELD_TARGETS: Record<
  DraftCheckField,
  DraftFieldTarget | null
> = {
  title: {
    step: 1,
    stepLabel: "General",
    label: "Title",
    selectors: ['[name="title"]'],
  },
  summary: {
    step: 1,
    stepLabel: "General",
    label: "Summary",
    selectors: ['[name="summary"]'],
  },
  description: {
    step: 1,
    stepLabel: "General",
    label: "Description",
    selectors: [
      '.editor.editable [contenteditable="true"]',
      '[contenteditable="true"]',
    ],
  },
  url: {
    step: 1,
    stepLabel: "General",
    label: "Link",
    selectors: ['[name="uRL"]'],
  },
  languages: {
    step: 2,
    stepLabel: "Details",
    label: "Languages",
    selectors: ["#input_languages"],
  },
  countries: {
    step: 2,
    stepLabel: "Details",
    label: "Location",
    selectors: ["#input_countries"],
  },
  commitment: {
    step: 2,
    stepLabel: "Details",
    label: "Time commitment",
    selectors: [
      '[name="commitmentIntervalCount"]',
      "#input_commitmentIntervalId",
    ],
  },
  dateStart: {
    step: 2,
    stepLabel: "Details",
    label: "Start date",
    selectors: ["#input_dateStart"],
  },
  dateEnd: {
    step: 2,
    stepLabel: "Details",
    label: "End date",
    selectors: ["#input_dateEnd"],
  },
  skills: {
    step: 4,
    stepLabel: "Skills",
    label: "Skills",
    selectors: ["#input_skills"],
  },
  // not on the partner form: Ziqi reads it from the course page
  difficulty: null,
  other: null,
};

export const draftFieldTarget = (
  field: DraftCheckField | string | null | undefined,
): DraftFieldTarget | null =>
  (field && DRAFT_FIELD_TARGETS[field as DraftCheckField]) || null;

/**
 * "Go to Link" for a field on the step on screen. A field on another step gets no button: moving
 * steps opens the editor's unsaved-changes dialog, which would throw the partner's typing away.
 */
export const goToFieldLabel = (
  field: DraftCheckField | string,
  currentStep?: number,
): string | null => {
  const target = draftFieldTarget(field);
  if (!target) return null;
  return currentStep === undefined || currentStep === target.step
    ? `Go to ${target.label}`
    : null;
};

/** For a field on another step: where to find it, e.g. "Start date is on the Details step". */
export const otherStepHint = (
  field: DraftCheckField | string,
  currentStep?: number,
): string | null => {
  const target = draftFieldTarget(field);
  if (!target || currentStep === undefined || currentStep === target.step)
    return null;
  return `${target.label} is on the ${target.stepLabel} step`;
};

// ── "Draft this for me": which suggestions the form can take as they come ──────
// Plain text fields only. Dates and lists need the partner's own choice on the form.

export type ApplicableDraftField = "title" | "summary" | "description" | "uRL";

const APPLICABLE_FIELDS: Partial<
  Record<DraftCheckField, ApplicableDraftField>
> = {
  title: "title",
  summary: "summary",
  description: "description",
  url: "uRL",
};

export const applicableFormField = (
  field: DraftCheckField | string,
): ApplicableDraftField | null =>
  APPLICABLE_FIELDS[field as DraftCheckField] ?? null;

/** The form's limits, so an applied suggestion never breaks validation on length alone. */
const APPLY_MAX_LENGTH: Record<ApplicableDraftField, number> = {
  title: 150,
  summary: 150,
  description: 20_000,
  uRL: 2048,
};

export const suggestionValueFor = (
  field: ApplicableDraftField,
  value: string,
): string => value.trim().slice(0, APPLY_MAX_LENGTH[field]);

// ── The draft the check sends ─────────────────────────────────────────────────
// Every wizard step has its own form, seeded with the whole listing, so each form also holds stale
// copies of the other steps' fields. Only a step's own fields are taken from it.

export type DraftValues = Partial<OpportunityRequestBase>;

export const DRAFT_STEP_FIELDS: Record<
  number,
  (keyof OpportunityRequestBase)[]
> = {
  1: ["title", "summary", "description", "uRL", "typeId", "categories"],
  2: [
    "languages",
    "countries",
    "commitmentIntervalId",
    "commitmentIntervalCount",
    "dateStart",
    "dateEnd",
  ],
  4: ["skills"],
};

export const pickDraftFields = (
  step: number,
  values: Record<string, unknown> | null | undefined,
): DraftValues => {
  const picked: Record<string, unknown> = {};
  for (const key of DRAFT_STEP_FIELDS[step] ?? [])
    if (values && key in values) picked[key] = values[key];
  return picked as DraftValues;
};

const isBlank = (value: unknown) =>
  value === null ||
  value === undefined ||
  (typeof value === "string" && value.trim() === "");

/** Nothing worth checking yet: no title, link, summary or description. */
export const isDraftEmpty = (draft: DraftValues): boolean =>
  isBlank(draft.title) &&
  isBlank(draft.uRL) &&
  isBlank(draft.summary) &&
  isBlank(draft.description);

/** A stable key for a draft, so an unchanged listing is not checked twice. */
export const draftKey = (draft: DraftValues): string =>
  JSON.stringify(
    Object.keys(draft)
      .sort()
      .map((key) => [key, draft[key as keyof DraftValues] ?? null]),
  );

// ── Debounced, cancellable check ──────────────────────────────────────────────

export const DRAFT_CHECK_DEBOUNCE_MS = 800;

export interface DebouncedCheckTimers {
  set: (callback: () => void, ms: number) => unknown;
  clear: (handle: unknown) => void;
}

const defaultTimers: DebouncedCheckTimers = {
  set: (callback, ms) => setTimeout(callback, ms),
  clear: (handle) => clearTimeout(handle as ReturnType<typeof setTimeout>),
};

/**
 * Runs `run` `delayMs` after the last `schedule`, aborting the request before it whenever a new
 * value arrives, so only the latest draft's answer ever reaches `onResult`.
 */
export const createDebouncedCheck = <I, R>(options: {
  run: (input: I, signal: AbortSignal) => Promise<R>;
  onStart?: (input: I) => void;
  onResult: (result: R, input: I) => void;
  onError: (error: unknown, input: I) => void;
  delayMs?: number;
  timers?: DebouncedCheckTimers;
}) => {
  const {
    run,
    onStart,
    onResult,
    onError,
    delayMs = DRAFT_CHECK_DEBOUNCE_MS,
    timers = defaultTimers,
  } = options;
  let timer: unknown = null;
  let controller: AbortController | null = null;

  const cancel = () => {
    if (timer !== null) timers.clear(timer);
    timer = null;
    controller?.abort();
    controller = null;
  };

  const schedule = (input: I) => {
    cancel();
    timer = timers.set(() => {
      timer = null;
      const current = new AbortController();
      controller = current;
      onStart?.(input);
      run(input, current.signal).then(
        (result) => {
          if (current.signal.aborted) return;
          controller = null;
          onResult(result, input);
        },
        (error: unknown) => {
          if (current.signal.aborted) return;
          controller = null;
          onError(error, input);
        },
      );
    }, delayMs);
  };

  return {
    schedule,
    cancel,
    /** True while a timer or a request is outstanding. */
    get pending() {
      return timer !== null || controller !== null;
    },
  };
};

/**
 * The API's reason when it cannot check this draft (400), e.g. "Choose the opportunity type to check
 * this listing", or a type Ziqi does not check. `null` for any other failure.
 */
export const notCheckableReason = (error: unknown): string | null => {
  const response = (error as { response?: { status?: number; data?: unknown } })
    ?.response;
  if (response?.status !== 400) return null;
  const data = response.data as
    | { message?: string }[]
    | { message?: string }
    | string
    | undefined;
  const message = Array.isArray(data)
    ? data[0]?.message
    : typeof data === "string"
      ? data
      : data?.message;
  return message?.trim() || "Ziqi does not check this kind of listing yet.";
};

/** The check is off on the API (CourseQuality:PrePublish:Enabled false): the panel hides. */
export const isSwitchedOff = (error: unknown): boolean =>
  typeof error === "object" &&
  error !== null &&
  (error as { response?: { status?: number } }).response?.status === 404;
