import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { FaExchangeAlt, FaRegClock, FaTimes } from "react-icons/fa";
import type { PathwayCourse } from "~/api/models/pathway";
import GradeChip from "~/components/CourseQuality/GradeChip";
import { asCourseGrade } from "~/lib/pathway";
import styles from "./JourneyMap.module.css";
import { hoursLabel, STEP_TYPE_LABELS } from "./journeyMap";
import { PartnerBadgeChip, STEP_TYPE_ICONS } from "./StepCard";

// ─────────────────────────────────────────────────────────────────────────────
// The swap sheet: a bottom sheet on phones, a pop-over on wider screens. It loads up to three
// alternatives, slides them in, and hands the pick back. A modal dialog: focus stays inside,
// Escape and the backdrop close it, and focus goes back to where it came from.
// ─────────────────────────────────────────────────────────────────────────────

type LoadState =
  | { kind: "loading" }
  | { kind: "ready"; alternatives: PathwayCourse[] }
  | { kind: "error" };

const FOCUSABLE =
  'button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])';

const SwapSheet: React.FC<{
  course: PathwayCourse;
  loadAlternatives: (opportunityId: string) => Promise<PathwayCourse[]>;
  onPick: (replacement: PathwayCourse) => Promise<void>;
  onClose: () => void;
}> = ({ course, loadAlternatives, onPick, onClose }) => {
  const [state, setState] = useState<LoadState>({ kind: "loading" });
  const [pickingId, setPickingId] = useState<string | null>(null);
  const [pickError, setPickError] = useState<string | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const closeRef = useRef<HTMLButtonElement>(null);
  const titleId = `swap-sheet-${course.opportunityId}`;

  const load = useCallback(() => {
    setState({ kind: "loading" });
    loadAlternatives(course.opportunityId)
      .then((alternatives) =>
        setState({ kind: "ready", alternatives: alternatives.slice(0, 3) }),
      )
      .catch(() => setState({ kind: "error" }));
  }, [course.opportunityId, loadAlternatives]);

  useEffect(load, [load]);

  // Focus in, page scroll off while open.
  useEffect(() => {
    closeRef.current?.focus();
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = overflow;
    };
  }, []);

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Escape") {
      e.stopPropagation();
      if (!pickingId) onClose();
      return;
    }
    if (e.key !== "Tab" || !panelRef.current) return;
    const items = Array.from(
      panelRef.current.querySelectorAll<HTMLElement>(FOCUSABLE),
    );
    if (items.length === 0) return;
    const first = items[0]!;
    const last = items[items.length - 1]!;
    if (e.shiftKey && document.activeElement === first) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
    }
  };

  const pick = async (replacement: PathwayCourse) => {
    setPickingId(replacement.opportunityId);
    setPickError(null);
    try {
      await onPick(replacement);
    } catch {
      setPickingId(null);
      setPickError("That did not work. Please try again.");
    }
  };

  if (typeof document === "undefined") return null;

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-6"
      onKeyDown={onKeyDown}
    >
      <div
        aria-hidden="true"
        className={`bg-overlay absolute inset-0 ${styles.backdrop}`}
        onClick={() => !pickingId && onClose()}
      />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className={`relative flex max-h-[85vh] w-full flex-col overflow-hidden rounded-t-3xl bg-white shadow-xl sm:max-w-lg sm:rounded-3xl ${styles.sheetUp}`}
      >
        <div
          aria-hidden="true"
          className="bg-gray mx-auto mt-2 h-1.5 w-10 rounded-full sm:hidden"
        />
        <div className="flex items-start gap-3 px-4 pt-3 pb-2 sm:px-6 sm:pt-5">
          <div className="min-w-0 flex-1">
            <p className="text-gray-dark text-xs font-semibold tracking-wide uppercase">
              Swap a step
            </p>
            <h2
              id={titleId}
              className="font-nunito text-lg leading-tight font-black text-black"
            >
              Instead of {course.title}
            </h2>
            <p className="text-gray-dark mt-1 text-sm">
              Up to three options at the same point of your path. Your other
              steps stay as they are.
            </p>
          </div>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            disabled={pickingId !== null}
            aria-label="Close"
            className="focus-visible:outline-purple text-gray-dark hover:bg-gray-light flex h-9 w-9 shrink-0 items-center justify-center rounded-full focus-visible:outline-2"
          >
            <FaTimes aria-hidden="true" className="h-4 w-4" />
          </button>
        </div>

        <div
          className="flex flex-col gap-3 overflow-y-auto px-4 pt-2 pb-5 sm:px-6"
          aria-busy={state.kind === "loading"}
        >
          {state.kind === "loading" &&
            [0, 1, 2].map((i) => (
              <div
                key={i}
                aria-hidden="true"
                className={`border-gray-light bg-gray-light h-28 rounded-2xl border ${styles.skeleton}`}
                style={{ animationDelay: `${i * 120}ms` }}
              />
            ))}
          {state.kind === "loading" && (
            <p className="sr-only" role="status">
              Loading other options
            </p>
          )}

          {state.kind === "error" && (
            <div role="alert" className="flex flex-col items-start gap-2 py-4">
              <p className="text-sm text-black">
                We could not load other options just now.
              </p>
              <button
                type="button"
                onClick={load}
                className="btn btn-sm bg-purple rounded-full text-white"
              >
                Try again
              </button>
            </div>
          )}

          {state.kind === "ready" && state.alternatives.length === 0 && (
            <p role="status" className="text-gray-dark py-4 text-sm">
              There are no other options at this point of your path right now.
              This step is the best match we found.
            </p>
          )}

          {state.kind === "ready" && state.alternatives.length > 0 && (
            <ul className="flex flex-col gap-3" aria-label="Other options">
              {state.alternatives.map((alt, i) => {
                const Icon = STEP_TYPE_ICONS[alt.type];
                const picking = pickingId === alt.opportunityId;
                return (
                  <li
                    key={alt.opportunityId}
                    className={`${styles.slideIn} ${picking ? styles.picked : ""}`}
                    style={{ "--delay": `${i * 110}ms` } as React.CSSProperties}
                  >
                    <div className="border-gray hover:border-purple-soft flex flex-col gap-1.5 rounded-2xl border p-3 transition-colors sm:p-4">
                      <div className="flex flex-wrap items-center gap-1.5">
                        <span className="bg-blue-light text-blue-dark inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-semibold">
                          <Icon aria-hidden="true" className="h-3 w-3" />
                          {STEP_TYPE_LABELS[alt.type]}
                        </span>
                        <GradeChip
                          grade={asCourseGrade(alt.grade)}
                          label={alt.gradeLabel}
                        />
                        <span className="text-gray-dark inline-flex items-center gap-1 text-xs">
                          <FaRegClock aria-hidden="true" className="h-3 w-3" />
                          {hoursLabel(alt.hours)}
                        </span>
                      </div>
                      <h3 className="font-nunito text-base leading-snug font-black text-black">
                        {alt.title}
                      </h3>
                      <p className="text-gray-dark flex flex-wrap items-center gap-2 text-xs">
                        <span className="font-semibold">
                          {alt.organizationName}
                        </span>
                        <PartnerBadgeChip level={alt.partnerBadge} />
                      </p>
                      {alt.skillsAdded.length > 0 && (
                        <p className="text-sm">
                          Adds{" "}
                          <b className="text-purple">
                            {alt.skillsAdded.join(", ")}
                          </b>
                        </p>
                      )}
                      {alt.reason && (
                        <p className="text-gray-dark text-sm leading-snug">
                          {alt.reason}
                        </p>
                      )}
                      <button
                        type="button"
                        onClick={() => void pick(alt)}
                        disabled={pickingId !== null}
                        aria-label={`Swap in ${alt.title}`}
                        className="focus-visible:outline-purple bg-purple mt-1 inline-flex min-h-9 items-center gap-1.5 self-start rounded-full px-4 text-xs font-semibold text-white transition-transform hover:scale-[1.03] focus-visible:outline-2 focus-visible:outline-offset-2 disabled:opacity-60"
                      >
                        <FaExchangeAlt aria-hidden="true" className="h-3 w-3" />
                        {picking ? "Swapping…" : "Swap this in"}
                      </button>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
          {pickError && (
            <p role="alert" className="text-pink text-sm">
              {pickError}
            </p>
          )}
        </div>
      </div>
    </div>,
    document.body,
  );
};

export default SwapSheet;
