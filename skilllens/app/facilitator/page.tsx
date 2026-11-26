// Facilitator view: flagged cases to follow up, and learner helpfulness ratings. Protected by middleware.ts.
import { revalidatePath } from "next/cache";
import { courses, templates } from "../../lib/data";
import { listFlags, listRatings, markReviewed } from "../../lib/store";

export const dynamic = "force-dynamic";

const STAGE_LABEL: Record<string, string> = {
  answer: "Learner answer",
  photo: "Learner photo",
  feedback: "AI feedback (replaced with rubric text)",
  challenge: "AI challenge (replaced with default details)",
  yoma: "Sample sent to Yoma",
};

async function review(form: FormData) {
  "use server";
  markReviewed(String(form.get("id")));
  revalidatePath("/facilitator");
}

const courseTitle = (id?: string) => courses.find((c) => c.id === id)?.title.en ?? "-";

export default function Facilitator() {
  const flags = listFlags();
  const open = flags.filter((f) => !f.reviewed);
  const ratings = listRatings();
  const byTemplate = templates
    .map((t) => {
      const rs = ratings.filter((r) => r.templateId === t.id);
      return { id: t.id, n: rs.length, avg: rs.length ? rs.reduce((s, r) => s + r.rating, 0) / rs.length : 0 };
    })
    .filter((r) => r.n > 0);

  return (
    <main className="mx-auto max-w-3xl space-y-6 p-4">
      <header className="rounded-3xl bg-brand p-5 text-paper">
        <h1 className="text-3xl">SkillLens facilitator view</h1>
        <p className="text-sm">Flags are kept for 72 hours. Excerpts have personal details removed. Raw answers and photos are never stored.</p>
      </header>

      <section aria-labelledby="flags" className="space-y-3">
        <h2 id="flags" className="text-2xl text-brand">Flagged cases ({open.length} to review)</h2>
        {flags.length === 0 && <p>No flagged cases.</p>}
        {flags.map((f) => (
          <article key={f.id} className={f.reviewed ? "card" : "card-alert"}>
            <p className="font-bold">
              {STAGE_LABEL[f.stage]} · {f.categories.map((c) => `${c.category} (severity ${c.severity})`).join(", ")}
            </p>
            <p className="text-sm">
              {new Date(f.at).toLocaleString("en-GB", { timeZone: "Africa/Lagos" })} WAT · {courseTitle(f.courseId)} · {f.templateId ?? "-"} · {f.lang ?? "-"}
            </p>
            {f.excerpt && <blockquote className="mt-2 border-l-2 border-ink/30 pl-2">{f.excerpt}</blockquote>}
            {f.reviewed ? (
              <p className="mt-2 text-sm font-bold">Reviewed</p>
            ) : (
              <form action={review} className="mt-2">
                <input type="hidden" name="id" value={f.id} />
                <button type="submit" className="btn-secondary w-auto">Mark reviewed</button>
              </form>
            )}
          </article>
        ))}
      </section>

      <section aria-labelledby="ratings" className="space-y-2">
        <h2 id="ratings" className="text-2xl text-brand">Was the feedback helpful? (1-5)</h2>
        {byTemplate.length === 0 ? (
          <p>No ratings yet.</p>
        ) : (
          <table className="w-full text-left">
            <thead>
              <tr><th scope="col">Challenge</th><th scope="col">Ratings</th><th scope="col">Average</th></tr>
            </thead>
            <tbody>
              {byTemplate.map((r) => (
                <tr key={r.id} className="border-t border-ink/20"><td>{r.id}</td><td>{r.n}</td><td>{r.avg.toFixed(1)}</td></tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </main>
  );
}
