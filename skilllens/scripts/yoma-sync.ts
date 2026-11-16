// `npm run yoma:sync`: refreshes the two Yoma files the app reads, from Yoma's public (anonymous) API.
// - data/yoma/skills.json: skill ids for the names in data/yoma/skill-map.json (GET /lookup/skill)
// - data/yoma/opportunities-snapshot.json: the "Next steps on Yoma" list, used when Yoma cannot be reached
// The app never needs Yoma to be up: it reads the skill cache, and falls back to the snapshot for opportunities.
import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { z } from "zod";
import map from "../data/yoma/skill-map.json";
import { fetchOpportunities, opportunityCourses } from "../lib/yoma/opportunities";

const Skill = z.object({ id: z.string().uuid(), name: z.string().min(1) });
const base = (process.env.YOMA_API_BASE_URL ?? "https://v3api.stage.yoma.world/api/v3").replace(/\/+$/, "");
const out = (file: string, data: unknown) => writeFileSync(join(__dirname, "../data/yoma", file), JSON.stringify(data, null, 2) + "\n");

async function lookup(name: string) {
  const url = `${base}/lookup/skill?${new URLSearchParams({ nameContains: name, pageNumber: "1", pageSize: "50" })}`;
  const res = await fetch(url, { signal: AbortSignal.timeout(15_000) });
  if (!res.ok) throw new Error(`Yoma skill lookup for "${name}" failed with status ${res.status}`);
  const { items } = (await res.json()) as { items: unknown[] };
  const exact = Skill.passthrough().array().parse(items).find((s) => s.name.toLowerCase() === name.toLowerCase());
  if (!exact) throw new Error(`No Yoma skill named exactly "${name}". Fix data/yoma/skill-map.json.`);
  return { id: exact.id, name: exact.name };
}

async function main() {
  const fetchedAt = new Date().toISOString().slice(0, 10);
  const names = [...new Set(Object.values(map.rubrics).flatMap((criteria) => Object.values(criteria).flat()))].sort();
  const skills = await Promise.all(names.map(lookup));
  out("skills.json", { source: base, fetchedAt, skills });
  console.log(`Cached ${skills.length} Yoma skills`);

  const courses: Record<string, unknown> = {};
  for (const id of opportunityCourses()) {
    courses[id] = await fetchOpportunities(id, base);
    console.log(`Snapshot ${id}: ${(courses[id] as unknown[]).length} opportunities`);
  }
  out("opportunities-snapshot.json", { source: base, fetchedAt, courses });
}

main().catch((e) => {
  console.error(e instanceof Error ? e.message : e);
  process.exit(1);
});
