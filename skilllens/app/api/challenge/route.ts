import { z } from "zod";
import { badRequest, handle, parseJson } from "../../../lib/api";
import { locale, templatesFor } from "../../../lib/data";
import { challengeSafely } from "../../../lib/pipeline";
import { Lang } from "../../../lib/schema";

const Body = z.object({ course: z.string(), lang: Lang });

export async function POST(req: Request) {
  const body = await parseJson(req, Body);
  if (!body) return badRequest("Invalid request");

  const options = templatesFor(body.course);
  if (options.length === 0) return badRequest("Unknown course");

  return handle("challenge", async () => {
    const template = options[Math.floor(Math.random() * options.length)];
    const seed = Math.floor(Math.random() * locale.slots.name.length);
    const { templateId, lang, values, text } = await challengeSafely(template, locale, body.lang, seed);
    return Response.json({ templateId, lang, values, text });
  });
}
