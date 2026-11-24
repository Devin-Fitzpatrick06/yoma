import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type {
  CourseOutcome,
  LearnerBadge,
  PathwayQuestion,
} from "~/api/models/pathway";
import {
  answersFromProfile,
  asCourseGrade,
  badgeProgress,
  canProceed,
  emptyAnswers,
  goalMeta,
  nextStageLabel,
  outcomeLines,
  pathProgress,
  sortBadges,
  toggleOption,
  toProfileRequest,
} from "./pathway";

const question = (
  id: PathwayQuestion["id"],
  keys: string[],
  multiSelect = false,
): PathwayQuestion => ({
  id,
  title: id,
  hint: "",
  multiSelect,
  options: keys.map((key) => ({ key, label: key, description: null })),
});

const QUESTIONS: PathwayQuestion[] = [
  question("goal", ["marketing", "web"]),
  question("background", ["InSchool", "FinishedSchool", "SomeStudy", "Degree"]),
  question("knownSkills", ["excel", "communication", "first-aid"], true),
  question("hoursPerWeek", ["2", "5", "10"]),
  question("country", ["ng", "za", "anywhere"]),
];

const outcome = (over: Partial<CourseOutcome> = {}): CourseOutcome => ({
  opportunityId: "o1",
  completers: 120,
  progressionRate: null,
  employmentRate: null,
  continuationRate: null,
  minimumGroupSize: 20,
  note: "",
  ...over,
});

describe("onboarding answers", () => {
  test("a single-select replaces the answer, a multi-select toggles", () => {
    let answers = emptyAnswers();
    answers = toggleOption(answers, QUESTIONS[0]!, "marketing");
    answers = toggleOption(answers, QUESTIONS[0]!, "web");
    assert.deepEqual(answers.goal, ["web"]);

    answers = toggleOption(answers, QUESTIONS[2]!, "excel");
    answers = toggleOption(answers, QUESTIONS[2]!, "first-aid");
    answers = toggleOption(answers, QUESTIONS[2]!, "excel");
    assert.deepEqual(answers.knownSkills, ["first-aid"]);
  });

  test("a single-select needs an answer; a multi-select may stay empty", () => {
    const answers = emptyAnswers();
    assert.equal(canProceed(answers, QUESTIONS[0]!), false);
    assert.equal(canProceed(answers, QUESTIONS[2]!), true);
  });

  test("builds the request, mapping the anywhere option to a null country", () => {
    const answers = {
      goal: ["marketing"],
      background: ["SomeStudy"],
      knownSkills: ["excel"],
      hoursPerWeek: ["5"],
      country: ["anywhere"],
    };
    assert.deepEqual(toProfileRequest(answers), {
      goalKey: "marketing",
      background: "SomeStudy",
      knownSkillKeys: ["excel"],
      hoursPerWeek: 5,
      countryId: null,
    });
    assert.equal(
      toProfileRequest({ ...answers, country: ["za"] })?.countryId,
      "za",
    );
  });

  test("an incomplete or unknown answer gives no request", () => {
    const answers = {
      goal: ["marketing"],
      background: ["SomeStudy"],
      knownSkills: [],
      hoursPerWeek: ["5"],
      country: ["za"],
    };
    assert.equal(toProfileRequest({ ...answers, goal: [] }), null);
    assert.equal(toProfileRequest({ ...answers, background: ["Phd"] }), null);
    assert.equal(toProfileRequest({ ...answers, hoursPerWeek: ["x"] }), null);
    assert.equal(toProfileRequest({ ...answers, country: [] }), null);
  });

  test("prefills from the saved profile and drops keys no longer offered", () => {
    const answers = answersFromProfile(
      {
        goalKey: "web",
        background: "Degree",
        knownSkillKeys: ["excel", "retired-skill"],
        hoursPerWeek: 10,
        countryId: null,
      },
      QUESTIONS,
    );
    assert.deepEqual(answers, {
      goal: ["web"],
      background: ["Degree"],
      knownSkills: ["excel"],
      hoursPerWeek: ["10"],
      country: ["anywhere"],
    });
    assert.deepEqual(
      answersFromProfile(
        {
          goalKey: "gone",
          background: "InSchool",
          knownSkillKeys: [],
          hoursPerWeek: 2,
          countryId: "ng",
        },
        QUESTIONS,
      ).goal,
      [],
    );
  });
});

describe("outcome lines", () => {
  test("show only the rates the API returned, as whole percentages", () => {
    assert.deepEqual(outcomeLines(null), []);
    assert.deepEqual(outcomeLines(outcome()), []);
    assert.deepEqual(outcomeLines(outcome({ progressionRate: 0.624 })), [
      "62% went on to another course",
    ]);
    assert.deepEqual(
      outcomeLines(outcome({ progressionRate: 0.5, employmentRate: 0.187 })),
      ["50% went on to another course", "19% found work afterwards"],
    );
  });
});

describe("page wording", () => {
  test("goal meta and progress", () => {
    const myYoma = {
      totalCourses: 10,
      totalHours: 63.6,
      estimatedWeeks: 13,
      completedCourses: 2,
      profile: { hoursPerWeek: 5 } as never,
    };
    assert.equal(
      goalMeta(myYoma),
      "10 courses · about 64 hours · roughly 13 weeks at 5 h a week",
    );
    assert.equal(pathProgress(myYoma), 20);
    assert.equal(pathProgress({ totalCourses: 0, completedCourses: 0 }), 0);
    assert.equal(
      goalMeta({
        ...myYoma,
        totalCourses: 1,
        estimatedWeeks: 1,
        totalHours: 1,
      }),
      "1 course · about 1 hour · roughly 1 week at 5 h a week",
    );
  });

  test("next stage", () => {
    assert.equal(
      nextStageLabel("Learn how marketing works"),
      "Next: learn how marketing works",
    );
    assert.equal(nextStageLabel(null), null);
    assert.equal(nextStageLabel("  "), null);
  });

  test("only A–D are grades", () => {
    assert.equal(asCourseGrade("B"), "B");
    assert.equal(asCourseGrade("Ungraded"), null);
    assert.equal(asCourseGrade(null), null);
  });

  test("badges: earned first, then by progress", () => {
    const badge = (
      key: LearnerBadge["key"],
      earned: boolean,
      progress: number | null,
    ): LearnerBadge => ({
      key,
      label: key,
      description: "",
      earned,
      dateEarned: earned ? "2026-11-01" : null,
      progress,
    });
    const sorted = sortBadges([
      badge("JobReady", false, 0.1),
      badge("HalfwayThere", false, 0.6),
      badge("Pathfinder", true, null),
    ]);
    assert.deepEqual(
      sorted.map((b) => b.key),
      ["Pathfinder", "HalfwayThere", "JobReady"],
    );
    assert.equal(badgeProgress(sorted[0]!), null);
    assert.equal(badgeProgress(sorted[1]!), 60);
  });
});
