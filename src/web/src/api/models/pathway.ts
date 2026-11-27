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
// Iteration 4 (all 404 unless the API switch Pathway:Enabled is on):
//   GET  /pathway/questions          (signed in) also returns answeredFromPreferences; questions already answered by
//                                    Yoma's own preferences (GET user/preferences) are left out
//   PUT  /pathway/me/steps/{opportunityId}/lock          User  body { locked: boolean } -> MyYoma
//   GET  /pathway/me/steps/{opportunityId}/alternatives  User  -> PathwayCourse[] (up to 3, same stage and type)
//   POST /pathway/me/steps/{opportunityId}/swap          User  body { replacementOpportunityId } -> MyYoma
//   POST /pathway/me/events          User  body PathwayStepEventRequest -> 204 (drop-off collection, W5)
//   GET  /pathway/health             Admin -> PathwayHealth
//   GET  /pathway/yoid/me            User  -> MyPathSummary (the YoID page's "My path" card)

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
  // keys of questions answered from Yoma's own preferences (goal, time, languages, skills, categories); empty when signed out
  answeredFromPreferences: string[];
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

// A step's kind. Learning = a course; ImpactAction and Task = practise; Job appears only in MyYoma.jobs.
export type PathwayStepType = "Learning" | "ImpactAction" | "Task";

// "Young people who completed this usually go on to …": share and n, hidden below the minimum group size.
export interface PathwayNextStep {
  opportunityId: string;
  title: string;
  share: number; // 0–1 of completers
  count: number;
}

// Drop-off is reserved for the pilot: no data yet, so status is always "Collecting" and rate null until then.
export interface StepDropOff {
  status: "Collecting" | "Available";
  rate: number | null; // share who started this step and did not continue, once Available
  note: string; // e.g. "Collecting from the pilot"
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
  // iteration 4
  type: PathwayStepType;
  partnerBadge: PartnerBadgeLevel | null; // the provider's public badge
  usuallyNext: PathwayNextStep[]; // empty below the minimum group size
  dropOff: StepDropOff;
  locked: boolean; // pinned by the youth; kept when the path is rebuilt
}

export type PathwayStepEventType =
  | "Shown"
  | "Started"
  | "Completed"
  | "Swapped"
  | "Dismissed";

export interface PathwayStepEventRequest {
  opportunityId: string;
  event: PathwayStepEventType;
}

export interface PathwayHealth {
  stepsTracked: number; // distinct path steps with at least one event
  events: number;
  dropOff: StepDropOff; // aggregate; "Collecting" until the pilot
  note: string;
}

export interface MyPathSummary {
  goalLabel: string;
  totalSteps: number;
  completedSteps: number;
  nextStep: { opportunityId: string; title: string } | null;
  stages: { name: string; completed: number; total: number }[];
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
  | "Platinum"
  | "NotEnoughGraded";

export interface PartnerBadge {
  organizationId: string;
  organizationName: string;
  level: PartnerBadgeLevel;
  gradedCourses: number;
  waitingForReview: number; // courses waiting for a reviewer, counted neither for nor against the partner
  shareTopGrades: number | null; // share of graded courses at A or B
  shareHeldBack: number | null; // share Not verified
  minimumGradedCourses: number;
  nextLevelHint: string | null; // what the partner would need to reach the next level
  reviewDue: string; // ISO date: badges are recomputed each quarter; the start of the next quarter
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
  // iteration 4
  startLabel: string; // the map's start, e.g. "Finished school · Nigeria"
  destinationLabel: string; // the map's end, e.g. "Your first digital marketing job"
}
