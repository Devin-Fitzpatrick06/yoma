import { z } from "zod";

export const Lang = z.enum(["en", "pcm"]);
export type Lang = z.infer<typeof Lang>;

export const Localized = z.object({ en: z.string().min(1), pcm: z.string().min(1) });

const Score = z.number().int().min(1).max(4);
const Scores = z.record(z.string(), Score);

// Seed data files: educator-editable, so validated on load.
export const Course = z.object({
  id: z.string(),
  title: Localized,
  objectives: z.array(Localized).min(1),
});

export const Criterion = z.object({
  id: z.string(),
  name: Localized,
  /** How lib/evidence.ts reads an answer for this criterion. */
  kind: z.enum(["understand", "action", "numbers", "communication", "concepts"]),
  /** Course ideas to look for (kind "concepts"), in the words the course uses. */
  keywords: z.array(z.string().min(2)).optional(),
  levels: z.array(Localized).length(4), // what each level looks like, 1-4; learners see these, never the number
  strength: Localized,
  improve: Localized,
});

export const Rubric = z.object({
  id: z.string(),
  courseId: z.string(),
  criteria: z.array(Criterion).min(3).max(4),
});

export const Template = z.object({
  id: z.string(),
  courseId: z.string(),
  rubricId: z.string(),
  /** The P2E lesson this challenge practises, so educators and learners can see where it comes from. */
  lesson: z.string().optional(),
  /** The lesson facts the challenge relies on, shown on the card, since learners will not remember them. */
  recap: Localized.optional(),
  /** A step-by-step way to start, shown when the learner says the question is confusing. */
  help: Localized.optional(),
  /** The course tools that fit this challenge: the "concepts" criterion looks for these and names them in its tips. */
  tools: z.object({ keywords: z.array(z.string().min(2)).min(1), named: Localized }).optional(),
  text: Localized,
});

export const Locale = z.object({
  id: z.string(),
  city: z.string(),
  slots: z.record(z.string(), z.array(z.string()).min(1)),
});

export const Sample = z.object({
  id: z.string(),
  templateId: z.string(),
  answer: z.string(),
  scores: Scores,
  split: z.enum(["example", "test"]),
});

// Values that fill a template's {slots}. They come from the model and travel via the browser, so stay strict.
export const SlotValue = z.string().trim().min(1).max(40).regex(/^[^{}<>]*$/, "no braces or angle brackets");
export const SlotValues = z.record(z.string(), SlotValue);

export const Challenge = z.object({
  templateId: z.string(),
  courseId: z.string(),
  rubricId: z.string(),
  lang: Lang,
  values: SlotValues,
  text: z.string().min(1),
});

// Model output. Scores stay internal; the learner only sees strengths + suggestion.
export const Feedback = z.object({
  scores: Scores,
  strengths: z.array(z.object({ criterionId: z.string(), text: z.string().min(1) })).min(1),
  suggestion: z.object({ criterionId: z.string(), text: z.string().min(1) }),
});

// A skill as Yoma stores it (GET /api/v3/lookup/skill). Ids come from data/yoma/skills.json.
export const YomaSkill = z.object({ id: z.string().uuid(), name: z.string().min(1) });

/** Level per criterion, 1-4. Learners see it as words ("Getting there"), never as a number or a total. */
export const Levels = z.record(z.string(), Score);

/** The Level 1 attempt, sent with the Level 2 answer so feedback can say what changed. */
export const PreviousAttempt = z.object({
  answer: z.string().trim().min(1).max(2000),
  levels: Levels,
  suggestionCriterionId: z.string().max(64),
});

// The saved Level 2 answer. It travels via the browser to P2E and Yoma, so it is re-validated on the server.
export const PracticeSample = z.object({
  courseId: z.string().max(64),
  templateId: z.string().max(64),
  lang: Lang,
  scenario: z.string().min(1).max(1000),
  answer: z.string().trim().min(1).max(2000),
  skills: z.array(YomaSkill).max(10),
  savedAt: z.string().datetime(),
  label: z.string().max(80),
});

export type Course = z.infer<typeof Course>;
export type YomaSkill = z.infer<typeof YomaSkill>;
export type PracticeSample = z.infer<typeof PracticeSample>;
export type Rubric = z.infer<typeof Rubric>;
export type Template = z.infer<typeof Template>;
export type Locale = z.infer<typeof Locale>;
export type Sample = z.infer<typeof Sample>;
export type Challenge = z.infer<typeof Challenge>;
export type Feedback = z.infer<typeof Feedback>;
export type Criterion = z.infer<typeof Criterion>;
export type PreviousAttempt = z.infer<typeof PreviousAttempt>;
