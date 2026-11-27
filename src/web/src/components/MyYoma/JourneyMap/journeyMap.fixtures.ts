import type {
  MyYoma,
  PartnerBadgeLevel,
  PathwayCourse,
  PathwayStage,
  PathwayStepType,
} from "~/api/models/pathway";
import { MY_YOMA_FIXTURE } from "~/components/MyYoma/myYoma.fixtures";

// ─────────────────────────────────────────────────────────────────────────────
// Example data for the journey map on the preview page (`/my-yoma-preview?view=map`): the shared
// My Yoma fixture with iteration 4's mixed steps (an impact action and a task), partner badges up
// to Platinum, a locked step, "usually go on to" on some steps, and swap alternatives. Only the
// preview page imports this file. Learners, IDs and figures are made up.
// ─────────────────────────────────────────────────────────────────────────────

const base = MY_YOMA_FIXTURE.stages.flatMap((s) => s.courses);
const byTitle = (title: string): PathwayCourse => {
  const found = base.find((c) => c.title === title);
  if (!found) throw new Error(`Fixture course missing: ${title}`);
  return found;
};

const COLLECTING = {
  status: "Collecting" as const,
  rate: null,
  note: "Collecting from the pilot",
};

const step = (
  id: string,
  over: Partial<PathwayCourse> & { title: string; type: PathwayStepType },
): PathwayCourse => ({
  opportunityId: id,
  organizationName: "Yoma partner",
  organizationLogoURL: null,
  difficulty: "Beginner",
  hours: 4,
  url: null,
  skillsAdded: [],
  reason: "",
  grade: null,
  gradeLabel: null,
  status: "NotStarted",
  completedCount: 0,
  outcome: null,
  dropOffFlag: false,
  partnerBadge: null,
  usuallyNext: [],
  dropOff: COLLECTING,
  locked: false,
  ...over,
});

const withIteration4 = (
  c: PathwayCourse,
  over: Partial<PathwayCourse>,
): PathwayCourse => ({ ...c, dropOff: COLLECTING, ...over });

const literacy = withIteration4(byTitle("Digital Literacy Essentials"), {
  partnerBadge: "Gold",
});
const fundamentals = withIteration4(byTitle("Fundamentals of Marketing"), {
  partnerBadge: "Silver",
});
const customer = withIteration4(byTitle("Understanding Your Customer"), {
  partnerBadge: "Gold",
  locked: true,
});
const social = withIteration4(byTitle("Social Media Marketing"), {
  partnerBadge: "Platinum",
});
const seo = withIteration4(byTitle("Search Engine Optimisation Basics"), {
  partnerBadge: "Bronze",
});
const analytics = withIteration4(byTitle("Google Analytics for Beginners"), {
  partnerBadge: "Platinum",
});

const impact = step("20000000-0000-4000-8000-000000000001", {
  title: "Run the socials for a community clean-up",
  type: "ImpactAction",
  organizationName: "Africa Code Week Volunteers",
  hours: 6,
  skillsAdded: ["Community management"],
  reason:
    "Real posts for a real cause: practise what the marketing course taught before you are hired to.",
  partnerBadge: "Platinum",
  completedCount: 214,
});

const task = step("20000000-0000-4000-8000-000000000002", {
  title: "Write three product posts for a small business",
  type: "Task",
  organizationName: "Yoma Marketplace",
  hours: 3,
  skillsAdded: ["Copywriting"],
  reason: "A short task that gives you work to show in an interview.",
  completedCount: 96,
});

// "Usually go on to" from the demo's synthetic learners (labelled on the card in the demo build).
fundamentals.usuallyNext = [
  {
    opportunityId: customer.opportunityId,
    title: customer.title,
    share: 0.46,
    count: 312,
  },
  {
    opportunityId: social.opportunityId,
    title: social.title,
    share: 0.21,
    count: 143,
  },
];
literacy.usuallyNext = [
  {
    opportunityId: fundamentals.opportunityId,
    title: fundamentals.title,
    share: 0.58,
    count: 1204,
  },
];
impact.usuallyNext = [
  {
    opportunityId: social.opportunityId,
    title: social.title,
    share: 0.38,
    count: 41,
  },
];
social.usuallyNext = [
  {
    opportunityId: analytics.opportunityId,
    title: analytics.title,
    share: 0.33,
    count: 288,
  },
];

const stages: PathwayStage[] = [
  {
    ...MY_YOMA_FIXTURE.stages[0]!,
    courses: [literacy],
  },
  {
    ...MY_YOMA_FIXTURE.stages[1]!,
    courses: [fundamentals, customer],
    leadsTo: "Practise in your community",
  },
  {
    order: 3,
    name: "Practise in your community",
    why: "Use what you learnt on real work for a cause or a small business, so you have something to show.",
    leadsTo: "Master the digital channels",
    shortened: false,
    courses: [impact, task],
  },
  {
    ...MY_YOMA_FIXTURE.stages[2]!,
    order: 4,
    courses: [social, seo],
  },
  {
    ...MY_YOMA_FIXTURE.stages[3]!,
    order: 5,
    name: "Prove it to employers",
    courses: [analytics],
  },
];

const all = stages.flatMap((s) => s.courses);

export const JOURNEY_MAP_FIXTURE: MyYoma = {
  ...MY_YOMA_FIXTURE,
  stages,
  totalCourses: all.length,
  totalHours: all.reduce((sum, c) => sum + (c.hours ?? 0), 0),
  estimatedWeeks: 9,
  completedCourses: all.filter((c) => c.status === "Completed").length,
};

const ALTERNATIVE_SOURCES: {
  title: string;
  organizationName: string;
  badge: PartnerBadgeLevel | null;
  grade: string | null;
  gradeLabel: string | null;
  hours: number;
  reason: string;
}[] = [
  {
    title: "Marketing Basics in 5 Lessons",
    organizationName: "Goodwall",
    badge: "Platinum",
    grade: "A",
    gradeLabel: "Top pick",
    hours: 3,
    reason: "Shorter, with the same skills; good if your week is full.",
  },
  {
    title: "Introduction to Brand Building",
    organizationName: "Alison",
    badge: "Silver",
    grade: "B",
    gradeLabel: "Strong",
    hours: 6,
    reason: "Goes deeper on branding, which many junior roles ask about.",
  },
  {
    title: "Marketing for Small Businesses",
    organizationName: "Jumia Academy",
    badge: "Gold",
    grade: "B",
    gradeLabel: "Strong",
    hours: 5,
    reason: "Uses examples from small African businesses.",
  },
];

/** Up to three alternatives for a step, of the same type, with stable ids per step. */
export const journeyAlternativesFor = (
  course: PathwayCourse,
): PathwayCourse[] =>
  ALTERNATIVE_SOURCES.map((alt, i) =>
    step(`alt-${i + 1}-${course.opportunityId}`, {
      title:
        course.type === "Learning"
          ? alt.title
          : `${["Volunteer", "Help out", "Take on"][i]}: ${course.title.toLowerCase()}`,
      type: course.type,
      organizationName: alt.organizationName,
      partnerBadge: alt.badge,
      grade: course.type === "Learning" ? alt.grade : null,
      gradeLabel: course.type === "Learning" ? alt.gradeLabel : null,
      hours: alt.hours,
      skillsAdded: course.skillsAdded,
      reason: alt.reason,
    }),
  );

/** The path with one step replaced, as the API's swap would return it. */
export const swapStep = (
  myYoma: MyYoma,
  opportunityId: string,
  replacement: PathwayCourse,
): MyYoma => ({
  ...myYoma,
  stages: myYoma.stages.map((s) => ({
    ...s,
    courses: s.courses.map((c) =>
      c.opportunityId === opportunityId ? replacement : c,
    ),
  })),
});

/** The path with one step's lock changed. */
export const lockStep = (
  myYoma: MyYoma,
  opportunityId: string,
  locked: boolean,
): MyYoma => ({
  ...myYoma,
  stages: myYoma.stages.map((s) => ({
    ...s,
    courses: s.courses.map((c) =>
      c.opportunityId === opportunityId ? { ...c, locked } : c,
    ),
  })),
});

/** The path with its first unfinished step completed: lets the preview show the celebration. */
export const completeNextStep = (myYoma: MyYoma): MyYoma => {
  let done = false;
  const stages = myYoma.stages.map((s) => ({
    ...s,
    courses: s.courses.map((c) => {
      if (done || c.status === "Completed") return c;
      done = true;
      return { ...c, status: "Completed" as const };
    }),
  }));
  return {
    ...myYoma,
    stages,
    completedCourses: stages
      .flatMap((s) => s.courses)
      .filter((c) => c.status === "Completed").length,
  };
};
