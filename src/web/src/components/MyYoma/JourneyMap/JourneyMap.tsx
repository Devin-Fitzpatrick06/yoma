import Link from "next/link";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import {
  FaBriefcase,
  FaCheck,
  FaChevronLeft,
  FaChevronRight,
  FaCrosshairs,
  FaFlagCheckered,
  FaIdCard,
} from "react-icons/fa";
import type { PathwayCourse, PathwayStage } from "~/api/models/pathway";
import { COURSE_QUALITY_DEMO } from "~/components/CourseQuality/DemoViewSwitcher";
import Confetti, { CONFETTI_MS } from "./Confetti";
import styles from "./JourneyMap.module.css";
import {
  celebrationsBetween,
  isStageComplete,
  journeySteps,
  orderedStages,
  stageKind,
  trailFraction,
  trailPath,
  type JourneyStep,
  type Point,
} from "./journeyMap";
import StepCard, { STEP_TYPE_ICONS } from "./StepCard";
import SwapSheet from "./SwapSheet";
import type { JourneyMapProps } from "./types";

// ─────────────────────────────────────────────────────────────────────────────
// The journey map (iteration 4, W7): one continuous journey from Start (the youth's YoID) through
// the stages to First job. Vertical on phones, a horizontal scroll-snapping track from `lg`. The
// trail is an SVG through every station's dot, measured after layout; it draws itself on the
// entrance while the cards fly in and lock onto it. Presentational: data and saving belong to the
// page, through `JourneyMapProps`.
// ─────────────────────────────────────────────────────────────────────────────

type Station =
  | { kind: "start"; key: string }
  | { kind: "stage"; key: string; stage: PathwayStage; index: number }
  | { kind: "step"; key: string; step: JourneyStep }
  | { kind: "end"; key: string };

const stepKey = (id: string) => `step-${id}`;
const stageKey = (order: number) => `stage-${order}`;

const ENTRANCE_START_MS = 300;
const ENTRANCE_STEP_MS = 160;

interface Geometry {
  width: number;
  height: number;
  points: Point[];
  keys: string[];
}

interface Burst {
  id: number;
  key: string;
  big: boolean;
}

const usePrefersReducedMotion = (): boolean => {
  const [reduced, setReduced] = useState(false);
  useEffect(() => {
    if (typeof window === "undefined" || !window.matchMedia) return;
    const query = window.matchMedia("(prefers-reduced-motion: reduce)");
    const update = () => setReduced(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);
  return reduced;
};

const without = <T,>(record: Record<string, T>, key: string) =>
  Object.fromEntries(Object.entries(record).filter(([k]) => k !== key));

const escapeId = (id: string) =>
  typeof CSS !== "undefined" && CSS.escape ? CSS.escape(id) : id;

/** A small padlock whose shackle clicks shut on the entrance. */
const Padlock: React.FC<{ animate: boolean }> = ({ animate }) => (
  <svg viewBox="0 0 10 10" className="h-2.5 w-2.5" aria-hidden="true">
    <path
      d="M3 5V3.6a2 2 0 0 1 4 0V5"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.4"
      strokeLinecap="round"
      className={animate ? styles.shackle : ""}
    />
    <rect
      x="1.8"
      y="4.6"
      width="6.4"
      height="4.6"
      rx="1.2"
      fill="currentColor"
    />
  </svg>
);

const JourneyMap: React.FC<JourneyMapProps> = ({
  myYoma,
  animateEntrance,
  onLock,
  loadAlternatives,
  onSwap,
  onStepEvent,
  onChangeAnswers,
}) => {
  const reducedMotion = usePrefersReducedMotion();
  const animate = animateEntrance;

  const stages = useMemo(() => orderedStages(myYoma), [myYoma]);
  const steps = useMemo(() => journeySteps(myYoma), [myYoma]);
  const stations = useMemo<Station[]>(() => {
    const list: Station[] = [{ kind: "start", key: "start" }];
    stages.forEach((stage, index) => {
      list.push({ kind: "stage", key: stageKey(stage.order), stage, index });
      steps
        .filter((s) => s.stageIndex === index)
        .forEach((step) =>
          list.push({
            kind: "step",
            key: stepKey(step.course.opportunityId),
            step,
          }),
        );
    });
    list.push({ kind: "end", key: "end" });
    return list;
  }, [stages, steps]);
  const stationKeys = stations.map((s) => s.key).join("|");

  const hereIndex = steps.findIndex((s) => s.course.status !== "Completed");
  const hereStep = hereIndex === -1 ? null : steps[hereIndex]!;
  const hereKey = hereStep ? stepKey(hereStep.course.opportunityId) : "end";
  const doneCount = steps.filter((s) => s.course.status === "Completed").length;

  // ── Geometry: the dots' centres, measured after layout and on every resize ──
  const trackRef = useRef<HTMLDivElement>(null);
  const scrollerRef = useRef<HTMLDivElement>(null);
  const dotRefs = useRef(new Map<string, HTMLElement>());
  const [geometry, setGeometry] = useState<Geometry | null>(null);

  const measure = useCallback(() => {
    const track = trackRef.current;
    if (!track) return;
    const box = track.getBoundingClientRect();
    const keys: string[] = [];
    const points: Point[] = [];
    stationKeys.split("|").forEach((key) => {
      const dot = dotRefs.current.get(key);
      if (!dot) return;
      const r = dot.getBoundingClientRect();
      keys.push(key);
      points.push({
        x: Math.round(r.left + r.width / 2 - box.left),
        y: Math.round(r.top + r.height / 2 - box.top),
      });
    });
    const next: Geometry = {
      width: Math.round(box.width),
      height: Math.round(box.height),
      points,
      keys,
    };
    setGeometry((previous) =>
      previous &&
      previous.width === next.width &&
      previous.height === next.height &&
      previous.keys.join("|") === next.keys.join("|") &&
      previous.points.every(
        (p, i) => p.x === next.points[i]?.x && p.y === next.points[i]?.y,
      )
        ? previous
        : next,
    );
  }, [stationKeys]);

  useLayoutEffect(() => {
    measure();
    const track = trackRef.current;
    if (!track || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(() => measure());
    observer.observe(track);
    return () => observer.disconnect();
  }, [measure]);

  const pointOf = (key: string): Point | null => {
    if (!geometry) return null;
    const i = geometry.keys.indexOf(key);
    return i === -1 ? null : geometry.points[i]!;
  };
  const trail = geometry ? trailPath(geometry.points) : "";
  const hereStation = geometry ? geometry.keys.indexOf(hereKey) : -1;
  const doneFraction =
    geometry && hereStation > 0
      ? trailFraction(geometry.points, hereStation)
      : 0;
  const herePoint = pointOf(hereKey);

  const drawMs = ENTRANCE_START_MS + stations.length * ENTRANCE_STEP_MS;
  const delayOf = (index: number) =>
    ENTRANCE_START_MS + index * ENTRANCE_STEP_MS;

  // ── Announcements for screen readers ─────────────────────────────────────
  const [announcement, setAnnouncement] = useState("");

  // ── Celebration: a step or stage that finished between renders ───────────
  const previousRef = useRef<typeof myYoma | null>(null);
  const [bursts, setBursts] = useState<Burst[]>([]);
  const [cheering, setCheering] = useState<string[]>([]);
  const burstSeq = useRef(0);

  useEffect(() => {
    const finished = celebrationsBetween(previousRef.current, myYoma);
    previousRef.current = myYoma;
    if (finished.steps.length === 0 && finished.stages.length === 0) return;

    const titles = finished.steps
      .map((id) => steps.find((s) => s.course.opportunityId === id))
      .filter(Boolean)
      .map((s) => s!.course.title);
    const stageNames = finished.stages
      .map((order) => stages.find((s) => s.order === order)?.name)
      .filter(Boolean);
    setAnnouncement(
      [
        titles.length ? `Well done! You finished ${titles.join(", ")}.` : "",
        stageNames.length ? `Stage complete: ${stageNames.join(", ")}.` : "",
      ]
        .filter(Boolean)
        .join(" "),
    );

    const keys = [
      ...finished.steps.map((id) => ({ key: stepKey(id), big: false })),
      ...finished.stages.map((order) => ({ key: stageKey(order), big: true })),
    ];
    setCheering(keys.map((k) => k.key));
    const cheerTimer = window.setTimeout(() => setCheering([]), 800);
    if (reducedMotion) return () => window.clearTimeout(cheerTimer);

    const added = keys.map((k) => ({ ...k, id: (burstSeq.current += 1) }));
    setBursts((b) => [...b, ...added]);
    const ids = added.map((b) => b.id);
    const timer = window.setTimeout(
      () => setBursts((b) => b.filter((x) => !ids.includes(x.id))),
      CONFETTI_MS,
    );
    return () => {
      window.clearTimeout(cheerTimer);
      window.clearTimeout(timer);
    };
    // Only a change of path should celebrate.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [myYoma]);

  // ── "Shown": each step once, when at least half of it enters the viewport ──
  const onStepEventRef = useRef(onStepEvent);
  onStepEventRef.current = onStepEvent;
  const shownRef = useRef(new Set<string>());
  const stepIds = steps.map((s) => s.course.opportunityId).join("|");

  useEffect(() => {
    const track = trackRef.current;
    if (!track || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          const id = (entry.target as HTMLElement).dataset.stepId;
          observer.unobserve(entry.target);
          if (!id || shownRef.current.has(id)) return;
          shownRef.current.add(id);
          onStepEventRef.current(id, "Shown");
        });
      },
      { threshold: 0.5 },
    );
    track.querySelectorAll<HTMLElement>("[data-step-id]").forEach((el) => {
      if (!shownRef.current.has(el.dataset.stepId ?? "")) observer.observe(el);
    });
    return () => observer.disconnect();
  }, [stepIds]);

  // ── Lock ───────────────────────────────────────────────────────────────────
  const [optimisticLocks, setOptimisticLocks] = useState<
    Record<string, boolean>
  >({});
  const [pendingLocks, setPendingLocks] = useState<string[]>([]);
  const [lockErrors, setLockErrors] = useState<Record<string, string>>({});
  const [lockAnim, setLockAnim] = useState<Record<string, number>>({});

  // Drop an optimistic value once the path from the page agrees with it.
  useEffect(() => {
    setOptimisticLocks((current) => {
      const entries = Object.entries(current).filter(([id, locked]) => {
        const step = steps.find((s) => s.course.opportunityId === id);
        return step !== undefined && step.course.locked !== locked;
      });
      return entries.length === Object.keys(current).length
        ? current
        : Object.fromEntries(entries);
    });
  }, [steps]);

  const isLocked = (course: PathwayCourse) =>
    optimisticLocks[course.opportunityId] ?? course.locked;

  const toggleLock = (course: PathwayCourse) => {
    const id = course.opportunityId;
    const locked = !isLocked(course);
    setOptimisticLocks((o) => ({ ...o, [id]: locked }));
    setPendingLocks((p) => [...p, id]);
    setLockAnim((a) => ({ ...a, [id]: (a[id] ?? 0) + 1 }));
    setLockErrors((e) => without(e, id));
    onLock(id, locked)
      .then(() =>
        setAnnouncement(
          locked
            ? `${course.title} is locked. It stays when your path is rebuilt.`
            : `${course.title} is unlocked.`,
        ),
      )
      .catch(() => {
        setOptimisticLocks((o) => without(o, id));
        setLockErrors((e) => ({
          ...e,
          [id]: "Could not save. Try again.",
        }));
      })
      .finally(() => setPendingLocks((p) => p.filter((x) => x !== id)));
  };

  // ── Swap ───────────────────────────────────────────────────────────────────
  const [swapFor, setSwapFor] = useState<PathwayCourse | null>(null);
  const [swappedIn, setSwappedIn] = useState<string | null>(null);
  const focusAfterSwap = useRef<string | null>(null);

  const closeSheet = () => {
    const id = swapFor?.opportunityId;
    setSwapFor(null);
    if (!id) return;
    window.requestAnimationFrame(() =>
      document
        .querySelector<HTMLElement>(
          `[data-step-id="${escapeId(id)}"] [data-swap]`,
        )
        ?.focus(),
    );
  };

  const pickReplacement = async (replacement: PathwayCourse) => {
    const from = swapFor;
    if (!from) return;
    await onSwap(from.opportunityId, replacement.opportunityId);
    focusAfterSwap.current = replacement.opportunityId;
    setSwappedIn(replacement.opportunityId);
    setSwapFor(null);
    setAnnouncement(`Swapped ${from.title} for ${replacement.title}.`);
  };

  // Once the swapped-in step is on the map, move focus to it and let it settle.
  useEffect(() => {
    const id = focusAfterSwap.current;
    if (!id) return;
    const link = document.querySelector<HTMLElement>(
      `[data-step-id="${escapeId(id)}"] a`,
    );
    if (!link) return;
    focusAfterSwap.current = null;
    link.focus({ preventScroll: true });
    link.closest("li")?.scrollIntoView({
      behavior: reducedMotion ? "auto" : "smooth",
      block: "nearest",
      inline: "nearest",
    });
    const timer = window.setTimeout(() => setSwappedIn(null), 900);
    return () => window.clearTimeout(timer);
  }, [steps, swappedIn, reducedMotion]);

  // ── Scrolling ──────────────────────────────────────────────────────────────
  const stationElement = (key: string) =>
    trackRef.current?.querySelector<HTMLElement>(
      `[data-station-key="${escapeId(key)}"]`,
    ) ?? null;

  // On wide screens, a returning youth starts the track at "You are here".
  useEffect(() => {
    if (animateEntrance) return;
    const scroller = scrollerRef.current;
    const station = stationElement(hereKey);
    if (!scroller || !station) return;
    if (scroller.scrollWidth <= scroller.clientWidth + 1) return;
    scroller.scrollLeft = Math.max(0, station.offsetLeft - 96);
    // Only on mount.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const jumpToHere = () => {
    const station = stationElement(hereKey);
    if (!station) return;
    station.scrollIntoView({
      behavior: reducedMotion ? "auto" : "smooth",
      block: "center",
      inline: "center",
    });
    station.querySelector<HTMLElement>("a")?.focus({ preventScroll: true });
  };

  const scrollTrack = (direction: 1 | -1) => {
    const scroller = scrollerRef.current;
    if (!scroller) return;
    scroller.scrollBy({
      left: direction * scroller.clientWidth * 0.8,
      behavior: reducedMotion ? "auto" : "smooth",
    });
  };

  // ── Render ────────────────────────────────────────────────────────────────
  const entranceStyle = (index: number) =>
    animate
      ? ({ "--delay": `${delayOf(index)}ms` } as React.CSSProperties)
      : undefined;
  const fly = animate ? styles.flyIn : "";

  const renderStation = (station: Station, index: number) => {
    const registerDot = (el: HTMLElement | null) => {
      if (el) dotRefs.current.set(station.key, el);
      else dotRefs.current.delete(station.key);
    };
    const cheer = cheering.includes(station.key) ? styles.cheer : "";
    const snap = animate ? styles.dotSnap : "";

    let width = "lg:w-72";
    let dot: React.ReactNode;
    let content: React.ReactNode;

    if (station.kind === "start") {
      width = "lg:w-64";
      dot = (
        <span
          ref={registerDot}
          className={`bg-purple flex h-12 w-12 items-center justify-center rounded-full text-white shadow-md ring-4 ring-white ${snap}`}
        >
          <FaIdCard aria-hidden="true" className="h-5 w-5" />
        </span>
      );
      const { yoId } = myYoma;
      content = (
        <section
          aria-labelledby="journey-start"
          className={`bg-purple flex flex-1 flex-col gap-2 rounded-2xl p-4 text-white shadow-sm ${fly}`}
        >
          <p className="text-purple-soft text-xs font-semibold tracking-wide uppercase">
            Start · your YoID
          </p>
          <h3
            id="journey-start"
            className="font-nunito text-lg leading-tight font-black text-white"
          >
            {myYoma.startLabel}
          </h3>
          <p className="text-sm text-white/85">
            {yoId.displayName}:{" "}
            {yoId.completedOpportunities.toLocaleString("en")}{" "}
            {yoId.completedOpportunities === 1
              ? "opportunity"
              : "opportunities"}{" "}
            done · {yoId.credentials.toLocaleString("en")}{" "}
            {yoId.credentials === 1 ? "credential" : "credentials"}
          </p>
          {yoId.skills.length > 0 && (
            <ul
              className="flex flex-wrap gap-1"
              aria-label="Skills on your YoID"
            >
              {yoId.skills.slice(0, 4).map((skill) => (
                <li
                  key={skill}
                  className="rounded-full bg-white/15 px-2 py-0.5 text-xs font-semibold"
                >
                  {skill}
                </li>
              ))}
            </ul>
          )}
        </section>
      );
    } else if (station.kind === "stage") {
      const { stage } = station;
      const complete = isStageComplete(stage);
      const doneHere = stage.courses.filter(
        (c) => c.status === "Completed",
      ).length;
      width = "lg:w-56";
      dot = (
        <span
          ref={registerDot}
          className={`font-nunito relative flex h-10 w-10 items-center justify-center rounded-full text-sm font-black ring-4 ring-white transition-colors duration-500 ${
            complete
              ? "bg-green text-white"
              : "border-purple-soft text-purple border-2 bg-white"
          } ${snap} ${cheer}`}
        >
          {complete ? (
            <FaCheck aria-hidden="true" className="h-4 w-4" />
          ) : (
            <span aria-hidden="true">{station.index + 1}</span>
          )}
        </span>
      );
      content = (
        <section
          aria-labelledby={`journey-stage-${stage.order}`}
          className={`border-purple-soft flex flex-1 flex-col gap-1.5 rounded-2xl border-2 border-dashed bg-white/70 p-3 sm:p-4 ${fly}`}
        >
          <p className="text-purple text-xs font-bold tracking-wide uppercase">
            {stageKind(stage, station.index, stages.length)} · Stage{" "}
            {station.index + 1}
          </p>
          <h3
            id={`journey-stage-${stage.order}`}
            className="font-nunito text-lg leading-tight font-black text-black"
          >
            {stage.name}
          </h3>
          {stage.why && (
            <p className="text-gray-dark text-sm leading-snug">{stage.why}</p>
          )}
          <p className="mt-auto flex flex-wrap gap-1.5 pt-1 text-xs font-semibold">
            {complete ? (
              <span className="bg-green inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-white">
                <FaCheck aria-hidden="true" className="h-2.5 w-2.5" />
                Stage complete
              </span>
            ) : (
              <span className="bg-gray-light text-gray-dark rounded-full px-2 py-0.5">
                {doneHere} of {stage.courses.length} done
              </span>
            )}
            {stage.shortened && (
              <span className="bg-blue-light text-blue-dark rounded-full px-2 py-0.5">
                Shortened: you know some of this
              </span>
            )}
          </p>
        </section>
      );
    } else if (station.kind === "step") {
      const { course, number } = station.step;
      const done = course.status === "Completed";
      const isHere = station.key === hereKey;
      const locked = isLocked(course);
      const Icon = STEP_TYPE_ICONS[course.type];
      dot = (
        <span
          ref={registerDot}
          className={`relative flex h-8 w-8 items-center justify-center rounded-full ring-4 ring-white transition-colors duration-500 ${
            done
              ? "bg-green text-white"
              : course.status === "InProgress"
                ? "bg-purple text-white"
                : "border-purple-soft text-purple border-2 bg-white"
          } ${snap} ${cheer}`}
        >
          {done ? (
            <FaCheck aria-hidden="true" className="h-3.5 w-3.5" />
          ) : (
            <Icon aria-hidden="true" className="h-3.5 w-3.5" />
          )}
          {animate && (
            <span
              aria-hidden="true"
              className={`border-purple absolute inset-0 rounded-full border-2 ${styles.snapRing}`}
            />
          )}
          {(locked || animate) && (
            <span
              aria-hidden="true"
              className={`text-purple absolute -top-1.5 -right-1.5 flex h-4 w-4 items-center justify-center rounded-full bg-white shadow ${
                locked ? (animate ? styles.lockStay : "") : styles.lockFlash
              }`}
            >
              <Padlock animate={animate} />
            </span>
          )}
        </span>
      );
      content = (
        <StepCard
          course={course}
          number={number}
          total={steps.length}
          isHere={isHere}
          locked={locked}
          lockPending={pendingLocks.includes(course.opportunityId)}
          lockError={lockErrors[course.opportunityId] ?? null}
          lockAnimKey={lockAnim[course.opportunityId] ?? 0}
          showDemoLabel={COURSE_QUALITY_DEMO}
          className={swappedIn === course.opportunityId ? styles.swapIn : fly}
          onOpen={() => {
            if (!done) onStepEvent(course.opportunityId, "Started");
          }}
          onToggleLock={() => toggleLock(course)}
          onSwap={() => setSwapFor(course)}
        />
      );
    } else {
      width = "lg:w-80";
      dot = (
        <span
          ref={registerDot}
          className={`bg-orange flex h-12 w-12 items-center justify-center rounded-full text-white shadow-md ring-4 ring-white ${snap}`}
        >
          <FaFlagCheckered aria-hidden="true" className="h-5 w-5" />
        </span>
      );
      content = (
        <section
          aria-labelledby="journey-end"
          className={`border-orange bg-orange-light flex flex-1 flex-col gap-2 rounded-2xl border-2 p-4 ${fly}`}
        >
          <p className="text-yellow text-xs font-bold tracking-wide uppercase">
            First job
          </p>
          <h3
            id="journey-end"
            className="font-nunito text-lg leading-tight font-black text-black"
          >
            {myYoma.destinationLabel}
          </h3>
          {myYoma.jobs.length > 0 ? (
            <ul
              className="flex flex-col gap-2"
              aria-label="Jobs this path leads to"
            >
              {myYoma.jobs.map((job) => (
                <li key={job.opportunityId}>
                  <Link
                    href={`/opportunities/${encodeURIComponent(job.opportunityId)}`}
                    className="focus-visible:outline-purple flex items-start gap-2 rounded-xl bg-white p-2.5 shadow-sm transition-transform hover:-translate-y-0.5 focus-visible:outline-2 focus-visible:outline-offset-2"
                  >
                    <span
                      aria-hidden="true"
                      className="bg-yellow-tint text-yellow flex h-7 w-7 shrink-0 items-center justify-center rounded-full"
                    >
                      <FaBriefcase className="h-3 w-3" />
                    </span>
                    <span className="flex min-w-0 flex-col">
                      <span className="text-sm leading-snug font-semibold text-black">
                        {job.title}
                      </span>
                      <span className="text-gray-dark text-xs">
                        {[job.organizationName, job.countries.join(", ")]
                          .filter(Boolean)
                          .join(" · ")}
                      </span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-gray-dark text-sm">
              Jobs that match your path appear here as they open on Yoma.
            </p>
          )}
        </section>
      );
    }

    return (
      <li
        key={station.key}
        data-station-key={station.key}
        style={entranceStyle(index)}
        className={`relative z-10 flex gap-3 lg:shrink-0 lg:snap-start lg:flex-col lg:gap-4 ${width}`}
      >
        <div className="flex w-12 shrink-0 justify-center pt-2 lg:h-14 lg:w-full lg:items-center lg:pt-0">
          {dot}
        </div>
        <div className="flex min-w-0 flex-1 lg:flex-none lg:grow">
          {content}
        </div>
      </li>
    );
  };

  return (
    <section
      aria-labelledby="journey-map-title"
      className="flex w-full min-w-0 flex-col gap-3"
    >
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <p className="text-gray-dark text-xs font-semibold tracking-wide uppercase">
            Goal: {myYoma.goalLabel}
          </p>
          <h2
            id="journey-map-title"
            className="font-nunito text-xl font-black text-black"
          >
            Your journey
          </h2>
          <p className="text-gray-dark text-sm">
            {doneCount} of {steps.length} steps done · {myYoma.totalHours} h ·
            about {myYoma.estimatedWeeks} weeks
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={jumpToHere}
            className="focus-visible:outline-purple bg-purple inline-flex min-h-9 items-center gap-1.5 rounded-full px-3 text-xs font-semibold text-white focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            <FaCrosshairs aria-hidden="true" className="h-3 w-3" />
            {hereStep ? "You are here" : "Your first job"}
          </button>
          <button
            type="button"
            onClick={onChangeAnswers}
            className="focus-visible:outline-purple border-gray text-purple hover:border-purple-soft inline-flex min-h-9 items-center rounded-full border bg-white px-3 text-xs font-semibold focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            Change my answers
          </button>
          <span className="hidden items-center gap-1 lg:inline-flex">
            <button
              type="button"
              onClick={() => scrollTrack(-1)}
              aria-label="Scroll the journey back"
              className="focus-visible:outline-purple border-gray text-purple hover:border-purple-soft flex h-9 w-9 items-center justify-center rounded-full border bg-white focus-visible:outline-2"
            >
              <FaChevronLeft aria-hidden="true" className="h-3 w-3" />
            </button>
            <button
              type="button"
              onClick={() => scrollTrack(1)}
              aria-label="Scroll the journey forward"
              className="focus-visible:outline-purple border-gray text-purple hover:border-purple-soft flex h-9 w-9 items-center justify-center rounded-full border bg-white focus-visible:outline-2"
            >
              <FaChevronRight aria-hidden="true" className="h-3 w-3" />
            </button>
          </span>
        </div>
      </div>

      <div
        ref={scrollerRef}
        role="region"
        aria-label="Journey map from your start to your first job"
        tabIndex={0}
        className="focus-visible:outline-purple w-full min-w-0 rounded-3xl focus-visible:outline-2 focus-visible:outline-offset-2 lg:snap-x lg:snap-proximity lg:scroll-px-6 lg:overflow-x-auto lg:bg-white/60 lg:shadow-sm"
      >
        <div ref={trackRef} className="relative lg:w-max">
          {geometry && trail && (
            <svg
              aria-hidden="true"
              className="pointer-events-none absolute top-0 left-0 overflow-visible"
              width={geometry.width}
              height={geometry.height}
              viewBox={`0 0 ${geometry.width} ${geometry.height}`}
              fill="none"
            >
              <path
                d={trail}
                pathLength={1}
                strokeWidth={6}
                strokeLinecap="round"
                className={`stroke-purple-tint ${animate ? styles.trailDraw : ""}`}
                style={
                  animate
                    ? ({
                        "--draw-ms": `${drawMs}ms`,
                        "--draw-delay": `${ENTRANCE_START_MS / 2}ms`,
                      } as React.CSSProperties)
                    : undefined
                }
              />
              {doneFraction > 0 && (
                <path
                  d={trail}
                  pathLength={1}
                  strokeWidth={6}
                  strokeLinecap="round"
                  strokeDashoffset={1 - doneFraction}
                  className={`stroke-green-dark ${styles.trailDone}`}
                  style={
                    animate
                      ? ({
                          "--done-delay": `${drawMs}ms`,
                        } as React.CSSProperties)
                      : { animation: "none" }
                  }
                />
              )}
            </svg>
          )}

          {herePoint && (
            <div
              aria-hidden="true"
              className={`pointer-events-none absolute top-0 left-0 z-20 ${styles.marker}`}
              style={{
                transform: `translate(${herePoint.x}px, ${herePoint.y}px)`,
              }}
            >
              <span
                className={`block ${animate ? styles.dotSnap : ""}`}
                style={
                  animate
                    ? ({ "--delay": `${drawMs}ms` } as React.CSSProperties)
                    : undefined
                }
              >
                <span
                  className={`bg-purple absolute -top-6 -left-6 block h-12 w-12 rounded-full ${styles.markerPulse}`}
                />
                <span className="border-purple absolute -top-6 -left-6 block h-12 w-12 rounded-full border-2" />
                <span className="bg-purple absolute -top-[3.6rem] left-0 hidden -translate-x-1/2 rounded-full px-2.5 py-1 text-xs font-bold whitespace-nowrap text-white shadow-md lg:block">
                  You are here
                </span>
              </span>
            </div>
          )}

          {bursts.map((burst) => {
            const p = pointOf(burst.key);
            return p ? (
              <Confetti key={burst.id} x={p.x} y={p.y} big={burst.big} />
            ) : null;
          })}

          <ol
            aria-label="Steps on your journey"
            className="relative flex flex-col gap-5 lg:flex-row lg:items-start lg:gap-6 lg:px-6 lg:pt-12 lg:pb-6"
          >
            {stations.map(renderStation)}
          </ol>
        </div>
      </div>

      <p className="sr-only" aria-live="polite">
        {announcement}
      </p>

      {swapFor && (
        <SwapSheet
          course={swapFor}
          loadAlternatives={loadAlternatives}
          onPick={pickReplacement}
          onClose={closeSheet}
        />
      )}
    </section>
  );
};

export default JourneyMap;
