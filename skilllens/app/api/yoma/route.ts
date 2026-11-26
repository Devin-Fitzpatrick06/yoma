// POST /api/yoma: sends a saved practice sample to the learner's Yoma profile.
// The sample comes back from the browser, so it is re-checked: known course and challenge, personal details stripped,
// screened again, and the skills replaced with the server's own mapping.
import { z } from "zod";
import { badRequest, handle, parseJson } from "../../../lib/api";
import { courses, templates } from "../../../lib/data";
import { aiConfig } from "../../../lib/ai/provider";
import { getSafety } from "../../../lib/safety/contentSafety";
import { redactPii } from "../../../lib/safety/pii";
import { PracticeSample } from "../../../lib/schema";
import { addFlag } from "../../../lib/store";
import { submitToYoma, yomaConfigFromEnv } from "../../../lib/yoma/client";
import { skillsFor } from "../../../lib/yoma/skills";

const Body = z.object({ sample: PracticeSample });

export async function POST(req: Request) {
  const body = await parseJson(req, Body);
  if (!body) return badRequest("Invalid practice sample");
  const template = templates.find((t) => t.id === body.sample.templateId);
  const course = courses.find((c) => c.id === body.sample.courseId);
  if (!template || !course || template.courseId !== course.id) return badRequest("Unknown course or challenge");

  return handle("yoma", async () => {
    const answer = redactPii(body.sample.answer).text;
    const check = await getSafety(process.env, aiConfig()).checkText(`${body.sample.scenario}\n${answer}`);
    if (check.flagged) {
      addFlag({ stage: "yoma", categories: check.categories, courseId: course.id, templateId: template.id, lang: body.sample.lang, excerpt: answer });
      return Response.json({ status: "flagged" });
    }
    const sample = { ...body.sample, answer, skills: skillsFor(template.rubricId) };
    // The learner's own Yoma token, forwarded by the host (P2E with Yoma sign-in). Unused in mock mode.
    const token = req.headers.get("authorization")?.replace(/^Bearer\s+/i, "") || undefined;
    const result = await submitToYoma(yomaConfigFromEnv(), sample, course.title.en, token);
    return Response.json({ status: "ok", ...result });
  });
}
