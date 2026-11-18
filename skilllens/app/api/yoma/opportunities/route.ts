// GET /api/yoma/opportunities?course=<id>: "Next steps on Yoma" after a learner saves. Live from Yoma, or the snapshot.
import { badRequest, handle } from "../../../../lib/api";
import { courses } from "../../../../lib/data";
import { opportunitiesFor } from "../../../../lib/yoma/opportunities";

export async function GET(req: Request) {
  const course = new URL(req.url).searchParams.get("course");
  if (!courses.some((c) => c.id === course)) return badRequest("Unknown course");
  return handle("yoma-opportunities", async () => Response.json(await opportunitiesFor(course!)));
}
