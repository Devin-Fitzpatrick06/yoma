import type { MyPathSummary } from "~/api/models/pathway";

// Example data for the fixture preview (`/ziqi-admin-preview`); not real youth.

export const MY_PATH_SUMMARY_FIXTURE: MyPathSummary = {
  goalLabel: "Digital marketing assistant",
  totalSteps: 11,
  completedSteps: 4,
  nextStep: {
    opportunityId: "00000000-0000-0000-0000-000000000105",
    title: "Social media marketing basics",
  },
  stages: [
    { name: "Foundations", completed: 3, total: 3 },
    { name: "Build skills", completed: 1, total: 3 },
    { name: "Practise", completed: 0, total: 3 },
    { name: "Prove it", completed: 0, total: 2 },
  ],
};

export const MY_PATH_SUMMARY_DONE_FIXTURE: MyPathSummary = {
  goalLabel: "Junior data analyst",
  totalSteps: 6,
  completedSteps: 6,
  nextStep: null,
  stages: [
    { name: "Foundations", completed: 2, total: 2 },
    { name: "Build skills", completed: 2, total: 2 },
    { name: "Prove it", completed: 2, total: 2 },
  ],
};
