// Shared contract for "My Yoma" pathways (iteration 3). Source of truth for the web side; the API's
// Yoma.Core.Domain.Pathway models mirror it field for field (camelCase JSON). Change only through the lead.
// Endpoints (base /api/v3):
//   GET  /pathway/questions          anonymous   -> PathwayQuestions
//   GET  /pathway/me/profile         User        -> PathwayProfile | 404 when not onboarded
//   PUT  /pathway/me/profile         User        body PathwayProfileRequest -> PathwayProfile
//   GET  /pathway/me                 User        -> MyYoma (404 when not onboarded)
//   GET  /pathway/badges/me          User        -> LearnerBadge[]
//   GET  /pathway/badges/organization/{organizationId}  anonymous -> PartnerBadge
//   GET  /pathway/outcomes/opportunity/{opportunityId}  anonymous -> CourseOutcome

export type PathwayBackground =
  | "InSchool"
  | "FinishedSchool"
  | "SomeStudy"
  | "Degree";

export interface PathwayOption {
  key: string;
  label: string;
  description: string | null;
}

export interface PathwayQuestion {
  id: "goal" | "background" | "knownSkills" | "hoursPerWeek" | "country";
  title: string;
  hint: string;
  multiSelect: boolean;
  options: PathwayOption[];
}

export interface PathwayQuestions {
  questions: PathwayQuestion[];
}

export interface PathwayProfileRequest {
  goalKey: string;
  background: PathwayBackground;
  knownSkillKeys: string[];
  hoursPerWeek: number; // 2 | 5 | 10; the question's option keys are "2", "5", "10"
  countryId: string | null; // null = anywhere; the country question's "anywhere" option has key "anywhere", others the country id
}

export interface PathwayProfile extends PathwayProfileRequest {
  goalLabel: string;
  countryName: string | null;
  dateCreated: string;
  dateModified: string;
}

export type PathwayStepStatus = "Completed" | "InProgress" | "NotStarted";

export interface CourseOutcome {
  opportunityId: string;
  completers: number; // youth who completed this course on Yoma
  // Share of completers who went on to complete a later Learning opportunity in the same goal (0–1), null below the minimum group size.
  progressionRate: number | null;
  // Share of completers with a completed Job opportunity or an employment credential after completing, null below the minimum group size.
  employmentRate: number | null;
  // Share of youth who completed the previous step of a pathway and then started this course, null when not computed.
  continuationRate: number | null;
  minimumGroupSize: number;
  note: string; // plain-language explanation, e.g. "Not enough learners yet to show outcomes"
}

export interface PathwayCourse {
  opportunityId: string;
  title: string;
  organizationName: string;
  organizationLogoURL: string | null;
  difficulty: string | null;
  hours: number | null;
  url: string | null;
  skillsAdded: string[]; // the skills this course contributes to the stage
  reason: string; // one sentence: why this course is at this point of the path
  grade: string | null; // Zeki grade A–D when graded
  gradeLabel: string | null; // the grade band's label, e.g. "Top pick" for A
  status: PathwayStepStatus; // for the signed-in youth
  completedCount: number; // completions on Yoma
  outcome: CourseOutcome | null;
  dropOffFlag: boolean; // continuation into this course is well below its siblings'
}

export interface PathwayStage {
  order: number;
  name: string;
  why: string;
  leadsTo: string | null;
  shortened: boolean; // the youth said they already have this
  courses: PathwayCourse[];
}

export interface PathwayJob {
  opportunityId: string;
  title: string;
  organizationName: string;
  countries: string[];
}

export interface YoIdSummary {
  displayName: string;
  completedOpportunities: number;
  skills: string[];
  credentials: number;
  zltoBalance: number | null;
}

export type LearnerBadgeKey =
  | "Pathfinder" // set a goal
  | "FirstStep" // completed the first course on the path
  | "StageComplete" // completed every course in a stage
  | "HalfwayThere" // half of the path completed
  | "JobReady" // every stage completed
  | "VerifiedSkill" // a skill backed by a verified completion on YoID
  | "TopPickLearner"; // completed three A-grade courses

export interface LearnerBadge {
  key: LearnerBadgeKey;
  label: string;
  description: string;
  earned: boolean;
  dateEarned: string | null;
  progress: number | null; // 0–1 towards the badge when not earned
}

export type PartnerBadgeLevel =
  | "None"
  | "Bronze"
  | "Silver"
  | "Gold"
  | "NotEnoughGraded";

export interface PartnerBadge {
  organizationId: string;
  organizationName: string;
  level: PartnerBadgeLevel;
  gradedCourses: number;
  shareTopGrades: number | null; // share of graded courses at A or B
  shareHeldBack: number | null; // share Not verified
  minimumGradedCourses: number;
  nextLevelHint: string | null; // what the partner would need to reach the next level
}

export interface MyYoma {
  profile: PathwayProfile;
  yoId: YoIdSummary;
  goalLabel: string;
  throughLine: string;
  totalCourses: number;
  totalHours: number;
  estimatedWeeks: number;
  completedCourses: number;
  stages: PathwayStage[];
  jobs: PathwayJob[];
  badges: LearnerBadge[];
}
