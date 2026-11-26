// GET /api/yoid?id=<yoidId>: the learner's YoID thread, one record per handoff item. MOCKED (lib/yoid.ts), sample data only.
import { badRequest } from "../../../lib/api";
import { learner } from "../../../lib/shared";
import { knownYoid, thread } from "../../../lib/yoid";

export const dynamic = "force-dynamic";

export function GET(req: Request) {
  const id = new URL(req.url).searchParams.get("id") ?? "";
  if (!knownYoid(id)) return badRequest("Unknown YoID");
  return Response.json({ yoidId: id, displayName: learner.displayName, sample: true, thread: thread(id) });
}
