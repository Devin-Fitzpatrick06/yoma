// The BBS x GenU shared mock data (data/shared/shared-mock-data.json, v1.1), copied unchanged from
// Anaghataneja-stack/pathfinder-prototype/mockups so all four prototypes show the same person, job, course and credential.
// FICTIONAL SAMPLE DATA: every screen that uses it says so. Only the parts SkillLens needs are validated here.
import { z } from "zod";
import json from "../data/shared/shared-mock-data.json";

const Shared = z.object({
  meta: z.object({ version: z.string(), warning: z.string(), handoffs: z.record(z.string(), z.string()) }),
  skills: z.record(z.string(), z.string()),
  youngPerson: z.object({ id: z.string(), yoidId: z.string(), displayName: z.string(), age: z.number(), languages: z.array(z.string()) }),
  partners: z.array(z.object({ id: z.string(), name: z.string() })),
  opportunities: z.array(z.object({ id: z.string(), type: z.string(), title: z.string(), org: z.string() }).passthrough()),
  learningPlan: z.object({
    id: z.string(),
    youngPersonId: z.string(),
    createdBy: z.string(),
    createdOn: z.string(),
    target: z.string(),
    steps: z.array(z.object({ kind: z.string(), opportunityId: z.string().optional(), opportunityIds: z.array(z.string()).optional(), why: z.string() })),
  }),
  skillChallenge: z.object({ id: z.string(), afterCourse: z.string(), skill: z.string(), task: z.string(), issues: z.string() }),
  credentials: z.array(
    z.object({ id: z.string(), yoidId: z.string(), title: z.string(), issuer: z.string(), skills: z.array(z.string()), issuedOn: z.string().optional(), status: z.string() }),
  ),
  application: z.object({ id: z.string(), opportunityId: z.string() }),
});

export const shared = Shared.parse(json);
export type SharedCredential = (typeof shared.credentials)[number];

export const learner = shared.youngPerson;
export const plan = shared.learningPlan;
export const challenge = shared.skillChallenge;
export const opportunity = (id: string) => shared.opportunities.find((o) => o.id === id);
export const credential = (id: string) => shared.credentials.find((c) => c.id === id);
export const partnerName = (id: string) => shared.partners.find((p) => p.id === id)?.name ?? id;
/** Skill label as the other prototypes show it, without the data file's "(NEW KEY)" note. */
export const skillLabel = (key: string) => (shared.skills[key] ?? key).replace(/\s*\(NEW KEY\)$/, "");

/** The SkillLens course this demo follows: Solar PV Installation Basics, the course before Halima's challenge. */
export const SOLAR_COURSE = "solar-pv";
