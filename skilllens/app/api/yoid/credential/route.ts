// POST /api/yoid/credential: MOCKED YoID write of the Skill Credential after a passed Level 2 and the learner's consent.
// The body carries only the YoID and the one-time pass id the feedback route issued; no answer text, no levels.
import { z } from "zod";
import { badRequest, parseJson } from "../../../../lib/api";
import { issueCredential } from "../../../../lib/yoid";

const Body = z.object({ yoidId: z.string().max(64), passId: z.string().max(400) });

export async function POST(req: Request) {
  const body = await parseJson(req, Body);
  if (!body) return badRequest("Invalid request");
  const out = issueCredential(body.yoidId, body.passId);
  if ("error" in out) return Response.json({ status: "rejected", reason: out.error }, { status: 409 });
  return Response.json({ status: "recorded", mocked: true, ...out });
}
