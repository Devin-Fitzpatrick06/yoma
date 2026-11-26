// Optional 1-5 "was this feedback helpful?" rating, shown after each feedback screen. Read by the facilitator view.
import { z } from "zod";
import { badRequest, parseJson } from "../../../lib/api";
import { templates } from "../../../lib/data";
import { addRating } from "../../../lib/store";

const Body = z.object({ templateId: z.string(), level: z.union([z.literal(1), z.literal(2)]), rating: z.number().int().min(1).max(5) });

export async function POST(req: Request) {
  const body = await parseJson(req, Body);
  if (!body || !templates.some((t) => t.id === body.templateId)) return badRequest("Invalid rating");
  addRating(body);
  return Response.json({ status: "ok" });
}
