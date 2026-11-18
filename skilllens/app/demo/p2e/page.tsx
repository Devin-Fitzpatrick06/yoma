"use client";

import { useEffect, useState } from "react";
import coursesJson from "../../../data/synthetic/courses.json";
import type { PracticeSample } from "../../../lib/schema";
import { Icon } from "../../../components/Icon";

// Mock P2E host page, for demos only. The real P2E course page embeds the same iframe.
// Courses come from the same file the embed uses, in P2E order: 1 Financial Literacy, 2 Job Readiness.
const COURSES = coursesJson.items.map((c, i) => ({ id: c.id, n: i + 1, title: c.title.en }));

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
      <summary className="flex cursor-pointer items-center gap-2 font-medium text-brand">
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
      <a className="mt-2 inline-block font-medium text-brand underline" href="/status">Details</a>
    </details>
  );
}

/** Round toggle, bottom centre: shows the same P2E page as a phone or a computer would. */
function ViewToggle({ view, onChange }: { view: View; onChange: (v: View) => void }) {
  return (
    <div role="radiogroup" aria-label="Preview as" className="fixed bottom-4 left-1/2 z-20 flex -translate-x-1/2 gap-1 rounded-full bg-white p-1.5 shadow-lg ring-1 ring-brand-soft">
      {(["phone", "computer"] as const).map((v) => (
        <button
          key={v}
          type="button"
          role="radio"
          aria-checked={view === v}
          onClick={() => onChange(v)}
          className={`flex min-h-12 items-center gap-2 rounded-full px-5 font-heading text-lg tracking-wide transition ${view === v ? "bg-brand text-white" : "text-brand hover:bg-brand-tint"}`}
        >
          <Icon name={v === "phone" ? "phone" : "monitor"} className="size-5" />
          {v === "phone" ? "Phone" : "Computer"}
        </button>
      ))}
    </div>
  );
}

export default function P2EDemo() {
  const [courseId, setCourseId] = useState(COURSES[0].id);
  const [open, setOpen] = useState(false);
  const [saved, setSaved] = useState<PracticeSample | null>(null);
  const [view, setView] = useState<View>("phone");
  const course = COURSES.find((c) => c.id === courseId)!;
  const wide = view === "computer";

  useEffect(() => {
    try {
      const v = localStorage.getItem(VIEW_KEY);
      if (v === "phone" || v === "computer") setView(v);
    } catch {
      // Storage blocked: start in phone view.
    }
    const onMessage = (e: MessageEvent) => {
      if (e.origin === window.location.origin && e.data?.type === "skilllens:saved") setSaved(e.data.sample);
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
    <div className={`flex min-h-full flex-col bg-white ${wide ? "" : "h-full"}`}>
      <header className="shrink-0 bg-brand text-white">
        <div className={`mx-auto flex flex-wrap items-center gap-x-6 gap-y-3 px-4 pt-4 ${wide ? "max-w-6xl" : ""}`}>
          <div className="flex items-center gap-2">
            <span className="grid size-9 place-items-center rounded-full bg-yellow text-brand"><Icon name="cap" className="size-5" /></span>
            <p className="font-heading text-xl tracking-wide">Passport to Earning <span className="text-sm text-white/80">(mock)</span></p>
          </div>
          <label className="block text-sm">
            Demo course{" "}
            <select
              className="ml-1 rounded-full bg-white px-3 py-1 text-brand"
              value={courseId}
              onChange={(e) => { setCourseId(e.target.value); setOpen(false); setSaved(null); }}
            >
              {COURSES.map((c) => <option key={c.id} value={c.id}>{c.n}. {c.title}</option>)}
            </select>
          </label>
        </div>
        {/* P2E's wave edge */}
        <svg viewBox="0 0 375 40" preserveAspectRatio="none" aria-hidden="true" className="-mb-px mt-3 block h-8 w-full">
          <path d="M0 12C70 30 130 30 200 14S320 0 375 18V40H0z" fill="#ffffff" />
        </svg>
      </header>

      {saved && (
        <aside role="status" className={`card-brand m-4 flex gap-3 ${wide ? "mx-auto w-full max-w-6xl" : ""}`}>
          <Icon name="check" className="size-7 text-brand" />
          <div>
            <p className="font-heading text-lg text-brand">Added to your P2E profile</p>
            <p className="text-sm">{saved.label}</p>
          </div>
        </aside>
      )}

      {!open ? (
        <main className={`mx-auto w-full px-4 pb-24 ${wide ? "grid max-w-6xl items-center gap-8 py-8 lg:grid-cols-2" : "space-y-5 text-center"}`}>
          <div className={`space-y-4 ${wide ? "text-left" : ""}`}>
            <span className={`grid size-16 place-items-center rounded-full bg-brand-tint text-brand ${wide ? "" : "mx-auto"}`}><Icon name="check" className="size-9" /></span>
            <h1 className={`font-heading text-brand ${wide ? "text-5xl" : "text-3xl"}`}>Course complete</h1>
            <p className="chip inline-block">{course.n}. {course.title}</p>
            <p>Well done! You finished all the lessons.</p>
          </div>
          <section className={`rounded-3xl bg-brand p-5 text-white ${wide ? "p-8" : ""}`}>
            <span className="mx-auto grid size-12 place-items-center rounded-full bg-yellow text-brand"><Icon name="star" className="size-7" /></span>
            <h2 className="mt-2 text-center font-heading text-2xl text-yellow">Level up</h2>
            <p className="mt-1 text-center">A 10-minute challenge with friendly feedback. Low stakes: no marks.</p>
            <button type="button" className="btn-yellow mt-4" onClick={() => setOpen(true)}>
              <Icon name="arrow" /> Challenge yourself
            </button>
          </section>
        </main>
      ) : (
        <iframe
          key={courseId}
          title="SkillLens level-up challenge"
          src={`/embed/skilllens?course=${courseId}&lang=en`}
          className={`block w-full border-0 ${wide ? "h-[calc(100vh-12rem)]" : "min-h-0 flex-1"}`}
        />
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
          <div className="aspect-[9/16] h-[calc(100vh-8.5rem)] max-w-full overflow-hidden rounded-[2.5rem] border-[10px] border-ink bg-white shadow-xl">
            <div className="h-full overflow-y-auto">{page}</div>
          </div>
        </div>
      )}
      <div className={`px-4 pb-24 ${wide ? "mx-auto max-w-6xl" : "mx-auto max-w-md"}`}><DemoStatus /></div>
      <ViewToggle view={view} onChange={changeView} />
    </div>
  );
}
