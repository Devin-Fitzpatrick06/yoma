"use client";

import { useEffect, useState } from "react";
import coursesJson from "../../../data/synthetic/courses.json";
import { Icon } from "../../../components/Icon";
import { challenge, learner, opportunity, partnerName, plan, SOLAR_COURSE } from "../../../lib/shared";
import type { PracticeSample } from "../../../lib/schema";
import type { HandoffRecord } from "../../../lib/yoid";

// Mock course host page on Yoma, for demos only: Phase 3 of the BBS x GenU journey with the shared sample learner
// (Halima, data/shared/shared-mock-data.json). The real host embeds the same iframe and passes a Yoma sign-in instead.
// Solar PV Installation Basics comes first; the other two sample courses stay available from the menu.
const COURSES = coursesJson.items.map((c) => ({ id: c.id, title: c.title.en }));

type View = "phone" | "computer";
const VIEW_KEY = "skilllens:demo-view";

interface Integration {
  name: string;
  mode: "real" | "simulated";
  healthy: boolean | null;
}

/** Demo-only strip: says which parts are real API calls right now, so a demo never passes a stand-in off as real. */
function DemoStatus() {
  const [list, setList] = useState<Integration[] | null>(null);
  useEffect(() => {
    fetch("/api/status").then((r) => r.json()).then((d) => setList(d.integrations)).catch(() => setList(null));
  }, []);
  if (!list) return null;
  return (
    <details className="card text-sm">
      <summary className="flex cursor-pointer items-center gap-2 font-semibold text-brand">
        <Icon name="info" className="size-5" />
        Demo: {list.filter((i) => i.mode === "real").length} of {list.length} integrations real
      </summary>
      <ul className="mt-1 space-y-1">
        {list.map((i) => (
          <li key={i.name}>
            {i.mode === "real" ? "Real" : "Simulated"}: {i.name}
            {i.healthy === false && " (unreachable)"}
          </li>
        ))}
      </ul>
      <a className="mt-2 inline-block font-semibold text-brand underline" href="/status">Details</a>
    </details>
  );
}

/** Round toggle, bottom centre: shows the same page as a phone or a computer would. */
function ViewToggle({ view, onChange }: { view: View; onChange: (v: View) => void }) {
  return (
    <div role="radiogroup" aria-label="Preview as" className="fixed bottom-4 left-1/2 z-20 flex -translate-x-1/2 gap-1 rounded-full bg-paper p-1.5 shadow-lg ring-1 ring-brand-soft">
      {(["phone", "computer"] as const).map((v) => (
        <button
          key={v}
          type="button"
          role="radio"
          aria-checked={view === v}
          onClick={() => onChange(v)}
          className={`flex min-h-12 items-center gap-2 rounded-full px-5 font-heading text-lg font-bold transition ${view === v ? "bg-brand text-paper" : "text-brand hover:bg-brand-tint"}`}
        >
          <Icon name={v === "phone" ? "phone" : "monitor"} className="size-5" />
          {v === "phone" ? "Phone" : "Computer"}
        </button>
      ))}
    </div>
  );
}

/** The handoff item that arrives from PathFinder: Halima's Learning Plan, with this course as the current step. */
function LearningPlan() {
  return (
    <section aria-labelledby="plan" className="card-brand space-y-2 text-left">
      <p className="text-sm font-semibold text-brand">Learning Plan {plan.id} · from {plan.createdBy}</p>
      <h2 id="plan" className="text-lg text-brand">Your plan for {opportunity(plan.target)?.title}</h2>
      <ol className="space-y-1">
        {plan.steps.map((s, i) => {
          const id = s.opportunityId ?? s.opportunityIds?.[0] ?? "";
          const here = id === challenge.afterCourse;
          return (
            <li key={i} className={`flex items-start gap-2 rounded-xl p-2 text-sm ${here ? "bg-paper font-semibold" : ""}`}>
              <span className={`grid size-6 shrink-0 place-items-center rounded-full ${here ? "bg-accent text-brand" : "bg-paper text-brand"}`}>{i + 1}</span>
              <span>{opportunity(id)?.title ?? id}{here && " (you are here)"}<br /><span className="font-normal text-muted">{s.why}</span></span>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

const STATE_LABEL: Record<HandoffRecord["state"], string> = { recorded: "On YoID", waiting: "Waiting", next: "Next" };

/** Halima's YoID as the thread through the journey: one record per handoff item. Mocked. */
function YoidThread({ items }: { items: HandoffRecord[] | null }) {
  if (!items) return null;
  return (
    <section aria-labelledby="yoid" className="card space-y-2 text-left">
      <h2 id="yoid" className="flex items-center gap-2 text-lg text-brand">
        <span className="icon-circle size-8"><Icon name="check" className="size-5" /></span>
        {learner.displayName}&apos;s YoID · {learner.yoidId}
      </h2>
      <p className="text-sm text-muted">Each prototype records its handoff item here. Mocked: no real YoID.</p>
      <ol className="space-y-2">
        {items.map((r) => (
          <li key={r.refId} className="rounded-xl bg-paper p-3 text-sm">
            <p className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-semibold text-brand">{r.item} · {r.refId}</span>
              <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${r.state === "recorded" ? "bg-success-light text-success-ink" : r.state === "waiting" ? "bg-accent-light text-brand" : "bg-surface text-muted"}`}>
                {STATE_LABEL[r.state]}
              </span>
            </p>
            <p>{r.title}</p>
            <p className="text-muted">{r.from} → {r.to}{r.recordedOn && ` · ${r.recordedOn}`} · {r.note}</p>
          </li>
        ))}
      </ol>
    </section>
  );
}

export default function HostDemo() {
  const [courseId, setCourseId] = useState(COURSES[0].id);
  const [open, setOpen] = useState(false);
  const [saved, setSaved] = useState<PracticeSample | null>(null);
  const [thread, setThread] = useState<HandoffRecord[] | null>(null);
  const [view, setView] = useState<View>("phone");
  const course = COURSES.find((c) => c.id === courseId)!;
  const wide = view === "computer";
  const solar = courseId === SOLAR_COURSE;

  useEffect(() => {
    try {
      const v = localStorage.getItem(VIEW_KEY);
      if (v === "phone" || v === "computer") setView(v);
    } catch {
      // Storage blocked: start in phone view.
    }
    fetch(`/api/yoid?id=${encodeURIComponent(learner.yoidId)}`).then((r) => r.json()).then((d) => setThread(d.thread)).catch(() => setThread(null));
    const onMessage = (e: MessageEvent) => {
      if (e.origin !== window.location.origin) return;
      if (e.data?.type === "skilllens:saved") setSaved(e.data.sample);
      if (e.data?.type === "skilllens:yoid" && Array.isArray(e.data.thread)) setThread(e.data.thread);
    };
    window.addEventListener("message", onMessage);
    return () => window.removeEventListener("message", onMessage);
  }, []);

  function changeView(v: View) {
    setView(v);
    try {
      localStorage.setItem(VIEW_KEY, v);
    } catch {
      // Not remembered, still works.
    }
  }

  const page = (
    <div className={`flex min-h-full flex-col bg-paper ${wide ? "" : "h-full"}`}>
      <header className="shrink-0 bg-brand text-paper">
        <div className={`mx-auto flex flex-wrap items-center gap-x-6 gap-y-3 px-4 py-4 ${wide ? "max-w-6xl" : ""}`}>
          <div className="flex items-center gap-2">
            <span className="icon-circle size-9"><Icon name="cap" className="size-5" /></span>
            <p className="font-heading text-xl font-extrabold">Courses on Yoma <span className="text-sm font-semibold text-paper/80">(mock-up)</span></p>
          </div>
          <p className="rounded-full bg-paper/15 px-3 py-1 text-sm">{learner.displayName} · sample data</p>
          <label className="block text-sm">
            Course{" "}
            <select
              className="ml-1 rounded-full bg-paper px-3 py-1 text-brand"
              value={courseId}
              onChange={(e) => { setCourseId(e.target.value); setOpen(false); setSaved(null); }}
            >
              {COURSES.map((c) => <option key={c.id} value={c.id}>{c.title}{c.id !== SOLAR_COURSE && " (other sample)"}</option>)}
            </select>
          </label>
        </div>
      </header>

      {saved && (
        <aside role="status" className={`card-brand m-4 flex gap-3 ${wide ? "mx-auto w-full max-w-6xl" : ""}`}>
          <Icon name="check" className="size-7 text-brand" />
          <div>
            <p className="font-heading text-lg font-bold text-brand">Practice saved</p>
            <p className="text-sm">{saved.label}</p>
          </div>
        </aside>
      )}

      {!open ? (
        <main className={`mx-auto w-full px-4 pb-24 pt-6 ${wide ? "grid max-w-6xl items-start gap-8 lg:grid-cols-2" : "space-y-5 text-center"}`}>
          <div className={`space-y-4 ${wide ? "text-left" : ""}`}>
            <span className={`icon-circle size-16 ${wide ? "" : "mx-auto"}`}><Icon name="check" className="size-9" /></span>
            <h1 className={`text-brand ${wide ? "text-5xl" : "text-3xl"}`}>Course complete</h1>
            <p className="chip inline-block">{course.title}{solar && ` · ${partnerName("org-p2e")}`}</p>
            <p>Well done, {learner.displayName.split(" ")[0]}! You finished all the lessons.</p>
            {solar && <LearningPlan />}
          </div>
          <div className="space-y-4">
            <section className={`rounded-3xl bg-brand p-5 text-paper ${wide ? "p-8" : ""}`}>
              <span className="icon-circle mx-auto size-12"><Icon name="star" className="size-7" /></span>
              <h2 className="mt-2 text-center text-2xl text-accent">Level up</h2>
              <p className="mt-1 text-center">A 10-minute challenge with friendly feedback. Pass it to add a Skill Credential to your YoID.</p>
              <button type="button" className="btn-accent mt-4" onClick={() => setOpen(true)}>
                <Icon name="arrow" /> Challenge yourself
              </button>
            </section>
            <YoidThread items={thread} />
          </div>
        </main>
      ) : (
        <div className={wide ? "mx-auto grid w-full max-w-6xl gap-4 px-4 lg:grid-cols-3" : "flex min-h-0 flex-1 flex-col"}>
          <iframe
            key={courseId}
            title="SkillLens level-up challenge"
            src={`/embed/skilllens?course=${courseId}&lang=en&learner=${learner.id}`}
            className={`block w-full border-0 ${wide ? "demo-frame-wide lg:col-span-2" : "min-h-0 flex-1"}`}
          />
          {wide && <div className="py-4"><YoidThread items={thread} /></div>}
        </div>
      )}
    </div>
  );

  return (
    <div className="min-h-screen bg-surface">
      {wide ? (
        page
      ) : (
        <div className="flex justify-center px-4 pb-24 pt-6">
          {/* 9:16 phone frame; the page scrolls inside it like on a real phone. */}
          <div className="device-frame">
            <div className="h-full overflow-y-auto">{page}</div>
          </div>
        </div>
      )}
      <div className={`space-y-3 px-4 pb-24 ${wide ? "mx-auto max-w-6xl" : "mx-auto max-w-md"}`}>
        {!wide && open && <YoidThread items={thread} />}
        <DemoStatus />
      </div>
      <ViewToggle view={view} onChange={changeView} />
    </div>
  );
}
