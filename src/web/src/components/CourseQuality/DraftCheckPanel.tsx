import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  IoChevronDown,
  IoChevronUp,
  IoInformationCircleOutline,
  IoSparklesOutline,
} from "react-icons/io5";
import type {
  DraftCheckField,
  DraftCheckResult,
} from "~/api/models/courseQuality";
import { checkDraftListing } from "~/api/services/courseQualityDraft";
import {
  applicableFormField,
  createDebouncedCheck,
  draftFieldTarget,
  draftKey,
  fixCountSummary,
  goToFieldLabel,
  isDraftEmpty,
  isSwitchedOff,
  scoreRingDash,
  sortFixes,
  suggestionValueFor,
  verdictLabel,
  verdictStyle,
  type ApplicableDraftField,
  type DraftValues,
} from "./DraftCheckPanel.helpers";

// ─────────────────────────────────────────────────────────────────────────────
// "Ziqi check" (iteration 4, W1): while a partner fills in a listing, Ziqi predicts the verdict it
// would give after publishing, and lists each fix with a jump to its field. It only advises: it
// never blocks saving. A sticky side panel on wide screens, a collapsible bottom drawer below `xl`.
// ─────────────────────────────────────────────────────────────────────────────

export type DraftCheckState =
  | { status: "empty" }
  | { status: "checking"; result: DraftCheckResult | null }
  | { status: "ready"; result: DraftCheckResult }
  | { status: "error"; result: DraftCheckResult | null };

/** Scroll to a form field and focus it, once its step is on screen. False when it is not found. */
export const focusDraftField = (field: DraftCheckField): boolean => {
  const target = draftFieldTarget(field);
  if (!target || typeof document === "undefined") return false;
  for (const selector of target.selectors) {
    const element = document.querySelector<HTMLElement>(selector);
    if (!element) continue;
    const reduceMotion = window.matchMedia?.(
      "(prefers-reduced-motion: reduce)",
    ).matches;
    element.scrollIntoView({
      block: "center",
      behavior: reduceMotion ? "auto" : "smooth",
    });
    element.focus({ preventScroll: true });
    return true;
  }
  return false;
};

const ScoreRing: React.FC<{ score: number; verdict: string }> = ({
  score,
  verdict,
}) => {
  const radius = 22;
  const { dashArray } = scoreRingDash(score, radius);
  return (
    <span className="relative inline-flex h-14 w-14 shrink-0 items-center justify-center">
      <svg
        viewBox="0 0 56 56"
        className="absolute inset-0 h-full w-full -rotate-90"
        aria-hidden="true"
      >
        <circle
          cx="28"
          cy="28"
          r={radius}
          fill="none"
          strokeWidth="6"
          className="stroke-gray"
        />
        <circle
          cx="28"
          cy="28"
          r={radius}
          fill="none"
          strokeWidth="6"
          strokeLinecap="round"
          stroke="currentColor"
          strokeDasharray={dashArray}
          className={`${verdictStyle(verdict).ring} transition-[stroke-dasharray] duration-500 motion-reduce:transition-none`}
        />
      </svg>
      <span className="font-nunito text-base font-black text-black">
        {score}
      </span>
      <span className="sr-only"> out of 100</span>
    </span>
  );
};

export interface DraftCheckPanelViewProps {
  state: DraftCheckState;
  /** The wizard step on screen, so "Go to" names another step when it is one. */
  currentStep?: number;
  onGoToField?: (field: DraftCheckField) => void;
  /** Absent: suggestions show without Apply buttons. */
  onApplySuggestion?: (field: ApplicableDraftField, value: string) => void;
  /** Drawer state below `xl`; uncontrolled when omitted. */
  defaultExpanded?: boolean;
  className?: string;
}

export const DraftCheckPanelView: React.FC<DraftCheckPanelViewProps> = ({
  state,
  currentStep,
  onGoToField,
  onApplySuggestion,
  defaultExpanded = false,
  className = "",
}) => {
  const headingId = useId();
  const bodyId = useId();
  const [expanded, setExpanded] = useState(defaultExpanded);
  const [applied, setApplied] = useState<Record<string, boolean>>({});

  const result = state.status === "empty" ? null : state.result;
  const fixes = useMemo(() => sortFixes(result?.fixes), [result]);
  const score = result ? Math.round(result.score) : 0;
  const verdict = result?.predictedStatus ?? "NotVerified";
  const style = verdictStyle(verdict);

  // a new answer is a new set of suggestions
  useEffect(() => setApplied({}), [result]);

  const status =
    state.status === "empty"
      ? "Add a title and a link to start"
      : state.status === "checking" && !result
        ? "Checking…"
        : result
          ? `${verdictLabel(verdict)} · ${fixCountSummary(result)}`
          : "Could not check right now";

  return (
    <aside
      aria-labelledby={headingId}
      className={`fixed inset-x-0 bottom-0 z-40 flex max-h-[75dvh] flex-col rounded-t-2xl bg-white shadow-[0_-4px_16px_rgba(0,0,0,0.12)] xl:sticky xl:inset-x-auto xl:top-24 xl:bottom-auto xl:z-auto xl:max-h-[calc(100dvh-7rem)] xl:rounded-lg xl:shadow-sm ${className}`}
    >
      {/* HEADER: the drawer's handle below xl */}
      <div className="flex items-center gap-2 px-4 pt-3 pb-2 xl:pt-4">
        <IoSparklesOutline
          aria-hidden="true"
          className="text-purple h-5 w-5 shrink-0"
        />
        <h2
          id={headingId}
          className="font-nunito text-base font-black text-black"
        >
          Ziqi check
        </h2>
        <span
          className={`text-gray-dark min-w-0 flex-1 truncate text-xs xl:hidden ${expanded ? "invisible" : ""}`}
          aria-hidden="true"
        >
          {status}
        </span>
        <button
          type="button"
          className="btn btn-ghost btn-sm btn-circle ml-auto shrink-0 xl:hidden"
          aria-expanded={expanded}
          aria-controls={bodyId}
          aria-label={expanded ? "Hide Ziqi check" : "Show Ziqi check"}
          onClick={() => setExpanded((value) => !value)}
        >
          {expanded ? (
            <IoChevronDown aria-hidden="true" className="h-5 w-5" />
          ) : (
            <IoChevronUp aria-hidden="true" className="h-5 w-5" />
          )}
        </button>
      </div>

      <div
        id={bodyId}
        className={`${expanded ? "flex" : "hidden"} min-h-0 flex-col gap-4 overflow-y-auto overscroll-contain px-4 pb-4 xl:flex`}
      >
        <p className="text-gray-dark text-xs">
          What Ziqi would say if you published now. It only advises: you can
          save at any time.
        </p>

        {/* VERDICT, read out when it changes */}
        <div aria-live="polite" aria-atomic="true" className="flex flex-col">
          {state.status === "empty" ? (
            <p className="bg-gray-light rounded-lg p-3 text-sm text-black">
              Add a title and a link, and Ziqi will check the listing as you
              type.
            </p>
          ) : !result ? (
            <p className="bg-gray-light rounded-lg p-3 text-sm text-black">
              {state.status === "error"
                ? "Ziqi could not check the listing right now. It will try again when you make a change."
                : "Ziqi is checking the listing…"}
            </p>
          ) : (
            <div
              className={`flex items-center gap-3 transition-opacity motion-reduce:transition-none ${
                state.status === "checking" ? "opacity-60" : ""
              }`}
            >
              <ScoreRing score={score} verdict={verdict} />
              <div className="flex min-w-0 flex-col gap-1">
                <span className="text-gray-dark text-xs">
                  Predicted verdict
                </span>
                <span
                  className={`inline-flex w-fit items-center rounded-full px-2.5 py-0.5 text-sm font-bold ${style.badge}`}
                >
                  {verdictLabel(verdict)}
                </span>
              </div>
            </div>
          )}
          {result && (
            <p className="mt-2 text-sm text-black">{result.summary}</p>
          )}
          {state.status === "checking" && result && (
            <p className="text-gray-dark mt-1 text-xs">Checking again…</p>
          )}
          {state.status === "error" && result && (
            <p className="text-gray-dark mt-1 text-xs">
              Could not check the latest change. Showing the last result.
            </p>
          )}
        </div>

        {result && !result.linkChecked && (
          <p className="text-gray-dark flex items-start gap-1.5 text-xs">
            <IoInformationCircleOutline
              aria-hidden="true"
              className="mt-px h-4 w-4 shrink-0"
            />
            <span>
              Link not confirmed yet. Ziqi could not reach it while you typed;
              this never counts against the listing.
            </span>
          </p>
        )}

        {/* FIXES */}
        {result && fixes.length > 0 && (
          <section className="flex flex-col gap-2" aria-label="Fixes">
            <h3 className="text-sm font-bold text-black">
              {fixes.length === 1 ? "One fix" : `${fixes.length} fixes`}
            </h3>
            <ol className="flex flex-col gap-2">
              {fixes.map((fix, index) => {
                const goTo = goToFieldLabel(fix.field, currentStep);
                return (
                  <li
                    key={`${fix.criterion}-${fix.field}-${index}`}
                    className={`flex flex-col gap-1 rounded-lg border p-3 ${
                      fix.hardGate
                        ? "border-yellow-light bg-yellow-tint/40"
                        : "border-gray bg-white"
                    }`}
                  >
                    {fix.hardGate && (
                      <span className="text-yellow text-xs font-bold">
                        Needed to be verified
                      </span>
                    )}
                    <p className="text-sm text-black">{fix.reason}</p>
                    {fix.recommendation && (
                      <p className="text-gray-dark text-sm">
                        {fix.recommendation}
                      </p>
                    )}
                    {goTo && onGoToField && (
                      <button
                        type="button"
                        className="text-purple mt-1 w-fit rounded text-sm font-semibold underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2"
                        onClick={() => onGoToField(fix.field)}
                      >
                        {goTo}
                      </button>
                    )}
                  </li>
                );
              })}
            </ol>
          </section>
        )}

        {result && fixes.length === 0 && (
          <p className="bg-green-light text-green rounded-lg p-3 text-sm">
            Nothing to fix. This listing looks ready.
          </p>
        )}

        {/* DRAFT THIS FOR ME */}
        {result && (
          <section className="border-gray flex flex-col gap-2 border-t pt-3">
            <h3 className="text-sm font-bold text-black">Draft this for me</h3>
            {result.suggestions === null ? (
              <p className="text-gray-dark text-xs">
                Available when AI is configured.
              </p>
            ) : result.suggestions.length === 0 ? (
              <p className="text-gray-dark text-xs">
                No suggestions for this listing.
              </p>
            ) : (
              <ul className="flex flex-col gap-2">
                {result.suggestions.map((suggestion, index) => {
                  const formField = applicableFormField(suggestion.field);
                  const target = draftFieldTarget(suggestion.field);
                  const key = `${suggestion.field}-${index}`;
                  return (
                    <li
                      key={key}
                      className="bg-gray-light flex flex-col gap-1 rounded-lg p-3"
                    >
                      <span className="text-gray-dark text-xs font-semibold">
                        {target?.label ?? "Suggestion"}
                      </span>
                      <p className="text-sm break-words whitespace-pre-line text-black">
                        {suggestion.value}
                      </p>
                      {formField && onApplySuggestion && (
                        <button
                          type="button"
                          className="btn btn-sm bg-purple hover:bg-purple-shade mt-1 w-fit border-none text-white disabled:opacity-60"
                          disabled={applied[key]}
                          onClick={() => {
                            onApplySuggestion(
                              formField,
                              suggestionValueFor(formField, suggestion.value),
                            );
                            setApplied((prev) => ({ ...prev, [key]: true }));
                          }}
                        >
                          {applied[key] ? "Applied" : "Apply"}
                          <span className="sr-only">
                            {" "}
                            to {target?.label ?? suggestion.field}
                          </span>
                        </button>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}
          </section>
        )}
      </div>
    </aside>
  );
};

export interface DraftCheckPanelProps {
  organizationId: string;
  /** The listing as last saved into the wizard; live edits arrive through `subscribe`. */
  initialDraft: DraftValues;
  /** Calls the listener with each change: only the fields that changed step owns. */
  subscribe: (listener: (patch: DraftValues) => void) => () => void;
  currentStep?: number;
  onGoToField?: (field: DraftCheckField) => void;
  onApplySuggestion?: (field: ApplicableDraftField, value: string) => void;
  className?: string;
}

/** The panel wired to the API: debounced, cancelled on change, hidden when the API switch is off. */
const DraftCheckPanel: React.FC<DraftCheckPanelProps> = ({
  organizationId,
  initialDraft,
  subscribe,
  currentStep,
  onGoToField,
  onApplySuggestion,
  className,
}) => {
  const [draft, setDraft] = useState<DraftValues>(initialDraft);
  const [state, setState] = useState<DraftCheckState>({ status: "empty" });
  const [switchedOff, setSwitchedOff] = useState(false);
  const lastKey = useRef<string | null>(null);

  // the wizard re-seeds its forms from the saved listing after each step
  useEffect(() => setDraft(initialDraft), [initialDraft]);

  useEffect(
    () => subscribe((patch) => setDraft((prev) => ({ ...prev, ...patch }))),
    [subscribe],
  );

  const checker = useMemo(
    () =>
      createDebouncedCheck<DraftValues, DraftCheckResult>({
        run: (input, signal) =>
          checkDraftListing(
            organizationId,
            { ...input, organizationId },
            signal,
          ),
        onStart: () =>
          setState((prev) => ({
            status: "checking",
            result: prev.status === "empty" ? null : prev.result,
          })),
        onResult: (result) => setState({ status: "ready", result }),
        onError: (error) => {
          if (isSwitchedOff(error)) {
            setSwitchedOff(true);
            return;
          }
          lastKey.current = null; // try again on the next change
          setState((prev) => ({
            status: "error",
            result: prev.status === "empty" ? null : prev.result,
          }));
        },
      }),
    [organizationId],
  );

  useEffect(() => () => checker.cancel(), [checker]);

  useEffect(() => {
    if (switchedOff) return;
    if (isDraftEmpty(draft)) {
      checker.cancel();
      lastKey.current = null;
      setState({ status: "empty" });
      return;
    }
    const key = draftKey(draft);
    if (key === lastKey.current) return;
    lastKey.current = key;
    checker.schedule(draft);
  }, [draft, checker, switchedOff]);

  if (switchedOff) return null;

  return (
    <DraftCheckPanelView
      state={state}
      currentStep={currentStep}
      onGoToField={onGoToField}
      onApplySuggestion={onApplySuggestion}
      className={className}
    />
  );
};

export default DraftCheckPanel;
