// MOCKED YoID: the learner's YoID is the thread through the BBS x GenU journey. Each prototype's handoff item is
// recorded on it: Saved Opportunity (Ziqi), Learning Plan (PathFinder), Skill Credential (SkillLens), Job Application
// (YoCV). Here the first two come from the shared mock data, and SkillLens writes the Skill Credential when a learner
// passes and consents. Nothing reaches Yoma: the live write is Yoma's own credential issuance (INTEGRATION.md section 8).
// shortcut: records are in memory per server instance, and the sample learner's record is shared by every demo visitor,
// so a write can vanish on restart or not show on another instance; the API returns the whole thread after each write so
// the screen never depends on a second read. Use Yoma's API before a pilot.
import { createHmac, randomBytes, randomUUID, timingSafeEqual } from "node:crypto";
import { passed } from "./credential";
import { credential, learner, opportunity, partnerName, plan, shared, skillLabel } from "./shared";

export type HandoffItem = "Saved Opportunity" | "Learning Plan" | "Skill Credential" | "Job Application";

export interface HandoffRecord {
  item: HandoffItem;
  refId: string;
  title: string;
  from: string;
  to: string;
  /** recorded: on the YoID; waiting: SkillLens has not written it yet; next: a later prototype writes it. */
  state: "recorded" | "waiting" | "next";
  recordedOn: string | null;
  note: string;
}

export interface IssuedCredential {
  id: string;
  yoidId: string;
  title: string;
  issuer: string;
  skills: string[];
  issuedOn: string;
  status: string;
}

const PASS_TTL_MS = 72 * 60 * 60 * 1000;
// A pass is a signed token, not a server-side record, so it still checks out when the feedback call and the credential
// call land on different serverless instances. PASS_SIGNING_SECRET must be set where there is more than one instance
// (Vercel); without it each process makes its own key, which is fine locally.
const KEY = process.env.PASS_SIGNING_SECRET || randomBytes(32).toString("hex");
const g = globalThis as unknown as { __skilllensYoid?: { used: Map<string, { at: number; credential: IssuedCredential }>; written: Map<string, IssuedCredential> } };
const state = (g.__skilllensYoid ??= { used: new Map(), written: new Map() });

const sign = (payload: string) => createHmac("sha256", KEY).update(payload).digest("base64url");

function readPass(token: string): { templateId: string; at: number; nonce: string } | null {
  const [payload, sig] = token.split(".");
  if (!payload || !sig) return null;
  const want = Buffer.from(sign(payload));
  const got = Buffer.from(sig);
  if (want.length !== got.length || !timingSafeEqual(want, got)) return null;
  try {
    const p = JSON.parse(Buffer.from(payload, "base64url").toString()) as { t: unknown; i: unknown; n: unknown };
    if (typeof p.t !== "string" || typeof p.i !== "number" || typeof p.n !== "string") return null;
    if (Date.now() - p.i > PASS_TTL_MS || p.i > Date.now() + 60_000) return null;
    return { templateId: p.t, at: p.i, nonce: p.n };
  } catch {
    return null;
  }
}


export const knownYoid = (yoidId: string) => yoidId === learner.yoidId;

/**
 * Called by the feedback route after it scores a Level 2 answer. A signed token, holding no answer text, and only for the
 * shared challenge (chal-001): it is the one that issues a credential (cred-002) in the shared journey, so the other sample
 * challenges never write ids to Halima's YoID that the other prototypes don't know.
 */
export function registerPass(templateId: string, levels: Record<string, number>): string | null {
  if (templateId !== shared.skillChallenge.id || !passed(Object.values(levels).map((level) => ({ level })))) return null;
  const payload = Buffer.from(JSON.stringify({ t: templateId, i: Date.now(), n: randomUUID() })).toString("base64url");
  return `${payload}.${sign(payload)}`;
}

/** Mocked YoID write of the Skill Credential. Only a pass the server scored itself, once, for a known YoID. */
export function issueCredential(yoidId: string, passId: string): { credential: IssuedCredential; thread: HandoffRecord[] } | { error: string } {
  if (!knownYoid(yoidId)) return { error: "Unknown YoID" };
  const pass = readPass(passId);
  if (!pass || pass.templateId !== shared.skillChallenge.id) return { error: "No passed challenge to record" };
  // A retry after a dropped connection gets the credential already written, not an error.
  // shortcut: the used-token list is per instance, so on several instances a token could be used twice within 72 h;
  // harmless for a mock (the same credential is written again), use Yoma's own idempotent issuance when live.
  const again = state.used.get(pass.nonce);
  if (again) return { credential: again.credential, thread: thread(yoidId) };
  for (const [n, u] of state.used) if (Date.now() - u.at > PASS_TTL_MS) state.used.delete(n);
  const issued = sharedCredential();
  state.used.set(pass.nonce, { at: Date.now(), credential: issued });
  state.written.set(yoidId, issued);
  return { credential: issued, thread: thread(yoidId) };
}

/** cred-002 exactly as the shared data has it, with skill labels as the other prototypes show them. */
export function sharedCredential(): IssuedCredential {
  const c = credential(shared.skillChallenge.issues)!;
  return { ...c, issuedOn: c.issuedOn ?? "", skills: c.skills.map(skillLabel), status: "verified (mocked)" };
}

/** The four handoff items on this YoID, in journey order. */
export function thread(yoidId: string): HandoffRecord[] {
  const job = opportunity(plan.target);
  const cred = state.written.get(yoidId);
  const shown = credential(shared.skillChallenge.issues);
  return [
    { item: "Saved Opportunity", refId: plan.target, title: job ? `${job.title}, ${partnerName(job.org)}` : plan.target, from: "Ziqi", to: "PathFinder", state: "recorded", recordedOn: null, note: "Recorded by Ziqi (sample data)" },
    { item: "Learning Plan", refId: plan.id, title: plan.steps.map((s) => opportunity(s.opportunityId ?? s.opportunityIds?.[0] ?? "")?.title ?? s.kind).join(" → "), from: plan.createdBy, to: "SkillLens", state: "recorded", recordedOn: plan.createdOn, note: "Recorded by PathFinder (sample data)" },
    {
      item: "Skill Credential",
      refId: shared.skillChallenge.issues,
      title: shown?.title ?? "Skill Credential",
      from: "SkillLens",
      to: "YoCV",
      state: cred ? "recorded" : "waiting",
      recordedOn: cred?.issuedOn ?? null,
      note: cred ? "Recorded by SkillLens (mocked write)" : "Written by SkillLens when the challenge is passed and the learner agrees",
    },
    { item: "Job Application", refId: shared.application.id, title: job?.title ?? shared.application.opportunityId, from: "YoCV", to: job ? partnerName(job.org) : "employer", state: "next", recordedOn: null, note: "Next: YoCV builds the CV and applies" },
  ];
}

/** Test helper. */
export const resetYoid = () => {
  state.used.clear();
  state.written.clear();
};
