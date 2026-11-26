import { z } from "zod";
import map from "../../data/yoma/skill-map.json";
import cache from "../../data/yoma/skills.json";
import { YomaSkill } from "../schema";

const rubricMap = z.record(z.string(), z.record(z.string(), z.array(z.string()).min(1))).parse(map.rubrics);
const byName = new Map(YomaSkill.array().parse(cache.skills).map((s) => [s.name.toLowerCase(), s]));

/** The Yoma skills a rubric practises, de-duplicated, in map order. Throws if the cache is stale. */
export function skillsFor(rubricId: string): YomaSkill[] {
  const criteria = rubricMap[rubricId];
  if (!criteria) throw new Error(`No Yoma skill mapping for rubric "${rubricId}" in data/yoma/skill-map.json`);
  return [...new Set(Object.values(criteria).flat())].map((name) => {
    const skill = byName.get(name.toLowerCase());
    if (!skill) throw new Error(`Yoma skill "${name}" is not in data/yoma/skills.json. Run npm run yoma:skills.`);
    return skill;
  });
}

export const mappedCriteria = (rubricId: string) => Object.keys(rubricMap[rubricId] ?? {});
