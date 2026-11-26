// GET /api/yoma/opportunities?course=<id>: "Next steps on Yoma" after a learner saves. Live from Yoma, or the snapshot.
import { badRequest, handle } from "../../../../lib/api";
import { courses } from "../../../../lib/data";
import { opportunitiesFor } from "../../../../lib/yoma/opportunities";
import { opportunity, partnerName, plan, SOLAR_COURSE } from "../../../../lib/shared";

export async function GET(req: Request) {
  const course = new URL(req.url).searchParams.get("course");
  if (!courses.some((c) => c.id === course)) return badRequest("Unknown course");
  // Halima's course: the job from the shared mock data (the Saved Opportunity), the same one every prototype shows.
  if (course === SOLAR_COURSE) {
    const job = opportunity(plan.target)!;
    return Response.json({ source: "sample", items: [{ id: job.id, title: job.title, type: "Job", organization: partnerName(job.org), url: String(job.link ?? "https://example.org") }] });
  }
  return handle("yoma-opportunities", async () => Response.json(await opportunitiesFor(course!)));
}
