"use client";

import { useEffect, useState } from "react";
import coursesJson from "../../../data/synthetic/courses.json";
import type { PracticeSample } from "../../../lib/schema";
import { Icon } from "../../../components/Icon";

// Mock P2E host page, for demos only. The real P2E course page embeds the same iframe.
// Courses come from the same file the embed uses, in P2E order: 1 Financial Literacy, 2 Job Readiness.
const COURSES = coursesJson.items.map((c, i) => ({ id: c.id, n: i + 1, title: c.title.en }));

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
    <details className="card m-4 text-sm">
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

export default function P2EDemo() {
  const [courseId, setCourseId] = useState(COURSES[0].id);
  const [open, setOpen] = useState(false);
  const [saved, setSaved] = useState<PracticeSample | null>(null);
  const course = COURSES.find((c) => c.id === courseId)!;

  useEffect(() => {
    const onMessage = (e: MessageEvent) => {
      if (e.origin === window.location.origin && e.data?.type === "skilllens:saved") setSaved(e.data.sample);
    };
    window.addEventListener("message", onMessage);
    return () => window.removeEventListener("message", onMessage);
  }, []);

  return (
    <div className="mx-auto min-h-screen max-w-md bg-white">
      <header className="bg-brand text-white">
        <div className="flex items-center gap-2 px-4 pt-4">
          <span className="grid size-9 place-items-center rounded-full bg-yellow text-brand"><Icon name="cap" className="size-5" /></span>
          <p className="font-heading text-xl tracking-wide">Passport to Earning <span className="text-sm text-white/80">(mock)</span></p>
        </div>
        <label className="mt-3 block px-4 text-sm">
          Demo course{" "}
          <select
            className="ml-1 rounded-full bg-white px-3 py-1 text-brand"
            value={courseId}
            onChange={(e) => { setCourseId(e.target.value); setOpen(false); setSaved(null); }}
          >
            {COURSES.map((c) => <option key={c.id} value={c.id}>{c.n}. {c.title}</option>)}
          </select>
        </label>
        {/* P2E's wave edge */}
        <svg viewBox="0 0 375 40" preserveAspectRatio="none" aria-hidden="true" className="-mb-px mt-3 block h-8 w-full">
          <path d="M0 12C70 30 130 30 200 14S320 0 375 18V40H0z" fill="#ffffff" />
        </svg>
      </header>

      {saved && (
        <aside role="status" className="card-brand m-4 flex gap-3">
          <Icon name="check" className="size-7 text-brand" />
          <div>
            <p className="font-heading text-lg text-brand">Added to your P2E profile</p>
            <p className="text-sm">{saved.label}</p>
          </div>
        </aside>
      )}

      {!open ? (
        <main className="space-y-5 px-4 pb-4 text-center">
          <span className="mx-auto grid size-16 place-items-center rounded-full bg-brand-tint text-brand"><Icon name="check" className="size-9" /></span>
          <h1 className="font-heading text-3xl text-brand">Course complete</h1>
          <p className="chip mx-auto inline-block">{course.n}. {course.title}</p>
          <p>Well done! You finished all the lessons.</p>
          <section className="rounded-3xl bg-brand p-5 text-white">
            <span className="mx-auto grid size-12 place-items-center rounded-full bg-yellow text-brand"><Icon name="star" className="size-7" /></span>
            <h2 className="mt-2 font-heading text-2xl text-yellow">Level up</h2>
            <p className="mt-1">A 10-minute challenge with friendly feedback. Low stakes: no marks.</p>
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
          className="block h-[85vh] w-full border-0"
        />
      )}
      <DemoStatus />
    </div>
  );
}
