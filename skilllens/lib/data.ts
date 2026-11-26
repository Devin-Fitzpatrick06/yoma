import coursesJson from "../data/synthetic/courses.json";
import localeJson from "../data/synthetic/locales/ng-kano.json";
import rubricsJson from "../data/synthetic/rubrics.json";
import samplesJson from "../data/synthetic/samples.json";
import templatesJson from "../data/synthetic/templates.json";
import { Course, Locale, Rubric, Sample, Template } from "./schema";

export const courses = Course.array().parse(coursesJson.items);
export const rubrics = Rubric.array().parse(rubricsJson.items);
export const templates = Template.array().parse(templatesJson.items);
export const samples = Sample.array().parse(samplesJson.items);
export const locale = Locale.parse(localeJson);

function find<T extends { id: string }>(list: T[], id: string, kind: string): T {
  const item = list.find((x) => x.id === id);
  if (!item) throw new Error(`Unknown ${kind} "${id}"`);
  return item;
}

export const getTemplate = (id: string) => find(templates, id, "template");
export const getRubric = (id: string) => find(rubrics, id, "rubric");

const GENERIC_TOOLS = { en: "an idea from the course", pcm: "one idea from the course" };

/** The rubric for one challenge: its "concepts" criterion looks for, and names, the tools that fit this challenge. */
export function rubricFor(template: Template): Rubric {
  const rubric = getRubric(template.rubricId);
  const named = template.tools?.named ?? GENERIC_TOOLS;
  const fill = (l: { en: string; pcm: string }) => ({ en: l.en.replace("{tools}", named.en), pcm: l.pcm.replace("{tools}", named.pcm) });
  return {
    ...rubric,
    criteria: rubric.criteria.map((c) =>
      c.kind !== "concepts" ? c : { ...c, keywords: template.tools?.keywords ?? c.keywords, levels: c.levels.map(fill), improve: fill(c.improve) },
    ),
  };
}
export const templatesFor = (courseId: string) => templates.filter((t) => t.courseId === courseId);

/** The ~10 educator-scored answers that go into the feedback prompt. The rest are held out for eval. */
export const examplesFor = (templateId: string) =>
  samples.filter((s) => s.templateId === templateId && s.split === "example");
