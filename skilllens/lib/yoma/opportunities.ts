// "Next steps on Yoma": live opportunities from Yoma's public search, shown after a learner saves a practice sample.
// Real API: POST {YOMA_API_BASE_URL}/opportunity/search (anonymous). When Yoma cannot be reached, a snapshot taken by
// `npm run yoma:sync` is shown instead, and the response says so (source: "snapshot").
import { z } from "zod";
import search from "../../data/yoma/opportunity-search.json";
import snapshot from "../../data/yoma/opportunities-snapshot.json";

export const Opportunity = z.object({
  id: z.string(),
  title: z.string(),
  type: z.string(),
  organization: z.string(),
  url: z.string().url(),
});
export type Opportunity = z.infer<typeof Opportunity>;

export interface OpportunityList {
  source: "live" | "snapshot";
  fetchedAt: string;
  items: Opportunity[];
}

const LIMIT = 4;
const CACHE_MS = 60 * 60 * 1000;
const RawItem = z.object({
  id: z.string(),
  title: z.string(),
  type: z.string().nullish(),
  organizationName: z.string().nullish(),
  yomaInfoURL: z.string().nullish(),
});

export const opportunityCourses = () => Object.keys(search.courses);
const queriesFor = (courseId: string) => (search.courses as Record<string, string[]>)[courseId];

/** Searches Yoma once per keyword, drops stage test data, de-duplicates and keeps the first LIMIT. */
export async function fetchOpportunities(courseId: string, baseUrl: string, fetchImpl: typeof fetch = fetch): Promise<Opportunity[]> {
  const queries = queriesFor(courseId);
  if (!queries) throw new Error(`No Yoma search words for course "${courseId}"`);
  const exclude = new RegExp(`\\b(${search.excludeTitles.join("|")})\\b`, "i");
  const seen = new Map<string, Opportunity>();
  for (const valueContains of queries) {
    const res = await fetchImpl(`${baseUrl}/opportunity/search`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ pageNumber: 1, pageSize: 10, valueContains }),
      signal: AbortSignal.timeout(8_000),
    });
    if (!res.ok) throw new Error(`Yoma opportunity search failed with status ${res.status}`);
    const { items } = z.object({ items: z.array(RawItem) }).parse(await res.json());
    for (const i of items) {
      if (seen.has(i.id) || exclude.test(i.title) || search.excludeTypes.includes(i.type ?? "")) continue;
      const url = i.yomaInfoURL ?? `${baseUrl.replace(/\/api\/v3$/, "").replace("v3api.", "")}/opportunities/${i.id}`;
      const parsed = Opportunity.safeParse({ id: i.id, title: i.title, type: i.type ?? "Opportunity", organization: i.organizationName ?? "", url });
      if (parsed.success) seen.set(i.id, parsed.data);
    }
    if (seen.size >= LIMIT * 3) break; // a few spare, so Nigeria-first ranking has something to choose from
  }
  // Learners are in Nigeria (the pilot locale): opportunities that mention it first, otherwise Yoma's order.
  const local = (o: Opportunity) => (`${o.title} ${o.organization}`.includes(search.preferCountry) ? 0 : 1);
  return [...seen.values()].sort((x, y) => local(x) - local(y)).slice(0, LIMIT);
}

const cache = new Map<string, { at: number; list: OpportunityList }>();

export async function opportunitiesFor(courseId: string, env: Record<string, string | undefined> = process.env): Promise<OpportunityList> {
  const hit = cache.get(courseId);
  if (hit && Date.now() - hit.at < CACHE_MS) return hit.list;
  const fromSnapshot = (): OpportunityList => ({
    source: "snapshot",
    fetchedAt: snapshot.fetchedAt,
    items: Opportunity.array().parse((snapshot.courses as Record<string, unknown[]>)[courseId] ?? []),
  });
  if (env.YOMA_OPPORTUNITIES === "snapshot") return fromSnapshot();
  try {
    const base = (env.YOMA_API_BASE_URL ?? "https://v3api.stage.yoma.world/api/v3").replace(/\/+$/, "");
    const list: OpportunityList = { source: "live", fetchedAt: new Date().toISOString(), items: await fetchOpportunities(courseId, base) };
    cache.set(courseId, { at: Date.now(), list });
    return list;
  } catch (e) {
    console.warn(`[skilllens] Yoma opportunities unavailable, using snapshot: ${e instanceof Error ? e.message : e}`);
    return fromSnapshot();
  }
}
