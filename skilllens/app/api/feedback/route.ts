import { z } from "zod";
import { badRequest, handle, parseJson } from "../../../lib/api";
import { examplesFor, getRubric, templates } from "../../../lib/data";
import { scoreSafely } from "../../../lib/pipeline";
import { Lang, SlotValues } from "../../../lib/schema";
import { fillTemplate } from "../../../lib/template";
import { skillsFor } from "../../../lib/yoma/skills";

const Body = z.object({
  templateId: z.string(),
  lang: Lang,
  values: SlotValues,
  answer: z.string().trim().min(1).max(2000),
});

export async function POST(req: Request) {
  const body = await parseJson(req, Body);
  if (!body) return badRequest("Invalid request");
  const { templateId, lang, values, answer } = body;

  const template = templates.find((t) => t.id === templateId);
  if (!template) return badRequest("Unknown challenge");

  // Rebuild the scenario from the educator template; only the short slot values come from the browser.
  let text: string;
  try {
    text = fillTemplate(template.text[lang], values);
  } catch {
    return badRequest("Invalid challenge values");
  }

  return handle("feedback", async () => {
    const outcome = await scoreSafely({
      challenge: { templateId, courseId: template.courseId, rubricId: template.rubricId, lang, values, text },
      rubric: getRubric(template.rubricId),
      examples: examplesFor(template.id),
      answer,
      lang,
    });
    if (outcome.status !== "ok") return Response.json({ status: outcome.status });
    // Scores stay on the server: the learner never sees an overall or per-criterion score.
    const { strengths, suggestion } = outcome.feedback;
    return Response.json({
      status: "ok",
      strengths,
      suggestion,
      answer: outcome.answer,
      redacted: outcome.redacted,
      skills: skillsFor(template.rubricId),
    });
  });
}
