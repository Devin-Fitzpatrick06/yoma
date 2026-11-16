// Flagged cases (for the facilitator view) and helpfulness ratings.
// Raw answers and photos are never kept: a flag holds only a short, personal-detail-stripped excerpt, for 72 hours.
// shortcut: in-memory, so it empties on restart and is per server instance; move to a database with a TTL index
// (e.g. Azure Cosmos DB) before running more than one instance.
import type { Category } from "./safety/contentSafety";

export type FlagStage = "answer" | "feedback" | "photo" | "challenge" | "yoma";

export interface Flag {
  id: string;
  at: string;
  stage: FlagStage;
  categories: { category: Category; severity: number }[];
  courseId?: string;
  templateId?: string;
  lang?: string;
  /** Stripped of personal details and cut to 280 characters. Empty for photos and model output. */
  excerpt: string;
  reviewed: boolean;
}

export interface Rating {
  at: string;
  templateId: string;
  level: 1 | 2;
  rating: number;
}

const RETENTION_MS = 72 * 60 * 60 * 1000;

// globalThis survives Next.js dev reloads and is shared by every route in the same process.
const g = globalThis as unknown as { __skilllens?: { flags: Flag[]; ratings: Rating[] } };
const state = (g.__skilllens ??= { flags: [], ratings: [] });

function purge(now = Date.now()) {
  state.flags = state.flags.filter((f) => now - Date.parse(f.at) < RETENTION_MS);
}

export function addFlag(flag: Omit<Flag, "id" | "at" | "reviewed">): Flag {
  purge();
  const full: Flag = { ...flag, excerpt: flag.excerpt.slice(0, 280), id: crypto.randomUUID(), at: new Date().toISOString(), reviewed: false };
  state.flags.unshift(full);
  // Never log the excerpt: logs outlive the 72-hour window.
  console.warn(`[skilllens] flagged ${full.stage}: ${full.categories.map((c) => c.category).join(", ")} (${full.id})`);
  return full;
}

export function listFlags(): Flag[] {
  purge();
  return state.flags;
}

export function markReviewed(id: string): boolean {
  const flag = state.flags.find((f) => f.id === id);
  if (flag) flag.reviewed = true;
  return Boolean(flag);
}

export function addRating(rating: Omit<Rating, "at">) {
  state.ratings.push({ ...rating, at: new Date().toISOString() });
}

export const listRatings = (): Rating[] => state.ratings;

/** For tests only. */
export function resetStore() {
  state.flags = [];
  state.ratings = [];
}
