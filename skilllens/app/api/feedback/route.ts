import { z } from "zod";
import { badRequest, handle, parseJson } from "../../../lib/api";
import { examplesFor, getRubric, templates } from "../../../lib/data";
import { scoreSafely } from "../../../lib/pipeline";
import { Lang, PreviousAttempt, SlotValues } from "../../../lib/schema";
import { fillTemplate } from "../../../lib/template";
import { skillsFor } from "../../../lib/yoma/skills";

const Body = z.object({
  templateId: z.string(),
  lang: Lang,
  values: SlotValues,
  answer: z.string().trim().min(1).max(2000),
  previous: PreviousAttempt.optional(),
});

export async function POST(req: Request) {
  const body = await parseJson(req, Body);
  if (!body) return badRequest("Invalid request");
  const { templateId, lang, values, answer, previous } = body;

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
    const rubric = getRubric(template.rubricId);
    const outcome = await scoreSafely({
      challenge: { templateId, courseId: template.courseId, rubricId: template.rubricId, lang, values, text },
      rubric,
      examples: examplesFor(template.id),
      answer,
      lang,
      previous,
    });
    if (outcome.status !== "ok") return Response.json({ status: outcome.status });
    // No total and no numbers on screen: each criterion is shown as a level in words, with what the next level looks like.
    const { strengths, suggestion, scores } = outcome.feedback;
    return Response.json({
      status: "ok",
      strengths,
      suggestion,
      levels: scores,
      rubric: rubric.criteria.map((c) => ({
        id: c.id,
        name: c.name[lang],
        level: scores[c.id],
        now: c.levels[scores[c.id] - 1][lang],
        next: scores[c.id] < 4 ? c.levels[scores[c.id]][lang] : null,
        before: previous?.levels[c.id] ?? null,
      })),
      answer: outcome.answer,
      redacted: outcome.redacted,
      skills: skillsFor(template.rubricId),
    });
  });
}
