import type {
  MyYoma,
  PartnerBadge,
  PathwayCourse,
  PathwayQuestions,
} from "~/api/models/pathway";

// ─────────────────────────────────────────────────────────────────────────────
// Example data for the My Yoma preview page (`/my-yoma-preview`), so the page renders while the
// API is built in parallel. Only the preview page imports this file: the real page never does.
// Copy follows the approved prototype; the learner and IDs are made up.
// ─────────────────────────────────────────────────────────────────────────────

export const QUESTIONS_FIXTURE: PathwayQuestions = {
  questions: [
    {
      id: "goal",
      title: "Where do you want to be?",
      hint: "Pick the work you would most like to be doing. You can change it later.",
      multiSelect: false,
      options: [
        {
          key: "marketing",
          label: "Digital marketer",
          description: "Grow brands online",
        },
        {
          key: "sales",
          label: "Sales & customer service",
          description: "Help customers, close deals",
        },
        {
          key: "web",
          label: "Web developer",
          description: "Build websites and apps",
        },
        {
          key: "data",
          label: "Data analyst",
          description: "Turn numbers into decisions",
        },
        {
          key: "business",
          label: "Run my own business",
          description: "Start and grow a business",
        },
        {
          key: "health",
          label: "Health & care worker",
          description: "Care for people and communities",
        },
      ],
    },
    {
      id: "background",
      title: "Where are you starting from?",
      hint: "This sets how far back your path starts.",
      multiSelect: false,
      options: [
        {
          key: "InSchool",
          label: "Still in school",
          description: "Secondary or high school",
        },
        {
          key: "FinishedSchool",
          label: "Finished school",
          description: "No further study yet",
        },
        {
          key: "SomeStudy",
          label: "Some study or training",
          description: "A course, certificate or diploma",
        },
        {
          key: "Degree",
          label: "University degree",
          description: "Or working in a related field",
        },
      ],
    },
    {
      id: "knownSkills",
      title: "What can you already do?",
      hint: "Choose any that apply. We will skip what you know.",
      multiSelect: true,
      options: [
        "Digital literacy",
        "Communication",
        "Excel",
        "Customer service",
        "Marketing basics",
        "Programming",
        "Bookkeeping",
        "First aid",
      ].map((label) => ({
        key: label.toLowerCase().replace(/\s+/g, "-"),
        label,
        description: null,
      })),
    },
    {
      id: "hoursPerWeek",
      title: "How much time can you give each week?",
      hint: "We use this to choose course lengths and estimate when you reach your goal.",
      multiSelect: false,
      options: [
        {
          key: "2",
          label: "Up to 2 hours a week",
          description: "Short courses first",
        },
        {
          key: "5",
          label: "About 5 hours a week",
          description: "A course every few weeks",
        },
        {
          key: "10",
          label: "10 or more hours a week",
          description: "Longer, deeper courses",
        },
      ],
    },
    {
      id: "country",
      title: "Where do you want to work?",
      hint: "We match jobs on Yoma in that country.",
      multiSelect: false,
      options: [
        { key: "c-ng", label: "Nigeria", description: null },
        { key: "c-gh", label: "Ghana", description: null },
        { key: "c-za", label: "South Africa", description: null },
        { key: "anywhere", label: "Somewhere else", description: null },
      ],
    },
  ],
};

let courseSeq = 0;
const course = (over: Partial<PathwayCourse>): PathwayCourse => {
  courseSeq += 1;
  const id = `00000000-0000-4000-8000-${String(courseSeq).padStart(12, "0")}`;
  return {
    opportunityId: id,
    title: "Course",
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
    ...over,
  };
};

const outcome = (
  opportunityId: string,
  completers: number,
  progressionRate: number | null,
  employmentRate: number | null,
) => ({
  opportunityId,
  completers,
  progressionRate,
  employmentRate,
  continuationRate: null,
  minimumGroupSize: 20,
  note:
    progressionRate === null && employmentRate === null
      ? "Not enough learners yet to show outcomes"
      : "Based on learners who completed this course on Yoma",
});

const withOutcome = (
  c: PathwayCourse,
  progression: number | null,
  employment: number | null,
): PathwayCourse => ({
  ...c,
  outcome: outcome(c.opportunityId, c.completedCount, progression, employment),
});

export const MY_YOMA_FIXTURE: MyYoma = {
  profile: {
    goalKey: "marketing",
    background: "SomeStudy",
    knownSkillKeys: ["communication"],
    hoursPerWeek: 5,
    countryId: "c-ng",
    goalLabel: "Digital marketer",
    countryName: "Nigeria",
    dateCreated: "2026-11-20T09:00:00Z",
    dateModified: "2026-11-20T09:00:00Z",
  },
  yoId: {
    displayName: "Amara O.",
    completedOpportunities: 2,
    skills: ["Communication", "Digital literacy", "Microsoft Office"],
    credentials: 1,
    zltoBalance: 150,
  },
  goalLabel: "Digital marketer",
  throughLine:
    "Each course adds a skill the next one uses. The path runs from foundations to the skills employers list for digital marketer roles, then to open jobs on Yoma.",
  totalCourses: 9,
  totalHours: 58,
  estimatedWeeks: 12,
  completedCourses: 2,
  stages: [
    {
      order: 1,
      name: "Get confident online",
      why: "Everything later assumes you can work comfortably with digital tools and write clearly.",
      leadsTo: "Learn how marketing works",
      shortened: true,
      courses: [
        withOutcome(
          course({
            title: "Digital Literacy Essentials",
            organizationName: "Goodwall",
            difficulty: "Beginner",
            hours: 3,
            skillsAdded: ["Digital literacy"],
            reason:
              "A short start that makes every later course easier to follow.",
            grade: "A",
            gradeLabel: "Top pick",
            status: "Completed",
            completedCount: 4210,
          }),
          0.62,
          null,
        ),
      ],
    },
    {
      order: 2,
      name: "Learn how marketing works",
      why: "Customers, positioning and the marketing mix: the base every channel builds on.",
      leadsTo: "Master the digital channels",
      shortened: false,
      courses: [
        withOutcome(
          course({
            title: "Fundamentals of Marketing",
            organizationName: "Alison",
            difficulty: "Beginner",
            hours: 6,
            skillsAdded: ["Marketing", "Branding"],
            reason:
              "Teaches the marketing mix that the channel courses in stage 3 apply.",
            grade: "B",
            gradeLabel: "Strong",
            status: "Completed",
            completedCount: 1876,
          }),
          0.48,
          null,
        ),
        course({
          title: "Understanding Your Customer",
          organizationName: "Coursera",
          difficulty: "Intermediate",
          hours: 8,
          skillsAdded: ["Consumer behaviour"],
          reason:
            "Knowing who buys and why is what makes later campaigns work.",
          grade: "B",
          gradeLabel: "Strong",
          status: "InProgress",
          completedCount: 643,
        }),
      ],
    },
    {
      order: 3,
      name: "Master the digital channels",
      why: "Apply the basics to social media, search and content, where most entry-level marketing jobs sit.",
      leadsTo: "Measure and sell",
      shortened: false,
      courses: [
        withOutcome(
          course({
            title: "Social Media Marketing",
            organizationName: "Meta Blueprint",
            difficulty: "Intermediate",
            hours: 7,
            skillsAdded: ["Social media marketing"],
            reason:
              "The channel most junior marketing roles in Lagos ask for first.",
            grade: "A",
            gradeLabel: "Top pick",
            completedCount: 2390,
          }),
          0.55,
          0.21,
        ),
        course({
          title: "Search Engine Optimisation Basics",
          organizationName: "Google Digital Skills for Africa",
          difficulty: "Beginner",
          hours: 5,
          skillsAdded: ["SEO"],
          reason: "Helps the content you write get found.",
          grade: "C",
          completedCount: 512,
          dropOffFlag: true,
        }),
        course({
          title: "Content Marketing Strategy",
          organizationName: "HubSpot Academy",
          difficulty: "Intermediate",
          hours: 9,
          skillsAdded: ["Content marketing", "Copywriting"],
          reason: "Brings writing and channels together into a plan.",
          grade: null,
          completedCount: 88,
        }),
      ],
    },
    {
      order: 4,
      name: "Measure and sell",
      why: "Employers hire marketers who can show results: analytics and sales close the loop.",
      leadsTo: null,
      shortened: false,
      courses: [
        withOutcome(
          course({
            title: "Google Analytics for Beginners",
            organizationName: "Google",
            difficulty: "Intermediate",
            hours: 6,
            skillsAdded: ["Google Analytics"],
            reason:
              "Shows whether the campaigns from stage 3 worked, which is what interviews ask about.",
            grade: "A",
            gradeLabel: "Top pick",
            completedCount: 1104,
          }),
          null,
          0.34,
        ),
        course({
          title: "Digital Advertising",
          organizationName: "Alison",
          difficulty: "Advanced",
          hours: 10,
          skillsAdded: ["Advertising"],
          reason: "Paid channels are the last step before most marketing jobs.",
          grade: "B",
          gradeLabel: "Strong",
          completedCount: 301,
        }),
        course({
          title: "Selling Online",
          organizationName: "Jumia Academy",
          difficulty: "Intermediate",
          hours: 4,
          skillsAdded: ["E-commerce", "Sales"],
          reason: "Turns attention into sales: the loop employers want closed.",
          grade: "D",
          completedCount: 0,
        }),
      ],
    },
  ],
  jobs: [
    {
      opportunityId: "10000000-0000-4000-8000-000000000001",
      title: "Junior Digital Marketing Associate",
      organizationName: "Paystack",
      countries: ["Nigeria"],
    },
    {
      opportunityId: "10000000-0000-4000-8000-000000000002",
      title: "Social Media Intern",
      organizationName: "Andela",
      countries: ["Nigeria", "Ghana"],
    },
    {
      opportunityId: "10000000-0000-4000-8000-000000000003",
      title: "Content Marketing Assistant",
      organizationName: "Flutterwave",
      countries: ["Nigeria"],
    },
  ],
  badges: [
    {
      key: "Pathfinder",
      label: "Pathfinder",
      description: "Set a goal",
      earned: true,
      dateEarned: "2026-11-20T09:00:00Z",
      progress: null,
    },
    {
      key: "FirstStep",
      label: "First step",
      description: "Completed the first course on your path",
      earned: true,
      dateEarned: "2026-11-21T09:00:00Z",
      progress: null,
    },
    {
      key: "StageComplete",
      label: "Stage complete",
      description: "Completed every course in a stage",
      earned: true,
      dateEarned: "2026-11-21T09:00:00Z",
      progress: null,
    },
    {
      key: "HalfwayThere",
      label: "Halfway there",
      description: "Completed half of your path",
      earned: false,
      dateEarned: null,
      progress: 0.44,
    },
    {
      key: "VerifiedSkill",
      label: "Verified skill",
      description: "A skill backed by a verified completion on YoID",
      earned: false,
      dateEarned: null,
      progress: 0.5,
    },
    {
      key: "TopPickLearner",
      label: "Top-pick learner",
      description: "Completed three A-grade courses",
      earned: false,
      dateEarned: null,
      progress: 0.33,
    },
    {
      key: "JobReady",
      label: "Job ready",
      description: "Completed every stage",
      earned: false,
      dateEarned: null,
      progress: 0.25,
    },
  ],
};

export const PARTNER_BADGE_FIXTURES: PartnerBadge[] = [
  {
    organizationId: "c0a7e000-0000-4000-8000-000000000001",
    organizationName: "Alison",
    level: "Silver",
    gradedCourses: 42,
    waitingForReview: 6,
    shareTopGrades: 0.64,
    shareHeldBack: 0.07,
    minimumGradedCourses: 5,
    nextLevelHint:
      "Gold needs three in four graded courses at A or B. Fixing the two courses with notes below would get you there.",
  },
  {
    organizationId: "c0a7e000-0000-4000-8000-000000000002",
    organizationName: "Small partner",
    level: "NotEnoughGraded",
    gradedCourses: 2,
    waitingForReview: 11,
    shareTopGrades: null,
    shareHeldBack: null,
    minimumGradedCourses: 5,
    nextLevelHint:
      "A badge is given once 5 of your courses are graded, so small partners are not judged on one or two courses.",
  },
];
