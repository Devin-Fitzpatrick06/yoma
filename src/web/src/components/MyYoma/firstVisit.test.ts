import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type { PathwayQuestion } from "~/api/models/pathway";
import { emptyAnswers, toProfileRequest } from "~/lib/pathway";
import {
  hiddenQuestionIds,
  indexOfQuestion,
  isPreviewAllowed,
  preferencesSummary,
  questionsToAsk,
  shouldRedirectToMyYoma,
  withPreferenceFallbacks,
  type FirstVisitRedirectInput,
} from "./firstVisit";

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
  question("background", ["InSchool", "Degree"]),
  question("knownSkills", ["excel", "comms"], true),
  question("hoursPerWeek", ["2", "5", "10"]),
  question("country", ["c-ng", "anywhere"]),
];

const ids = (list: PathwayQuestion[]) => list.map((q) => q.id);

describe("questionsToAsk", () => {
  test("asks everything when nothing came from preferences", () => {
    assert.deepEqual(
      ids(questionsToAsk(QUESTIONS, [], emptyAnswers(), false)),
      ["goal", "background", "knownSkills", "hoursPerWeek", "country"],
    );
  });

  test("leaves out questions answered from preferences", () => {
    assert.deepEqual(
      ids(
        questionsToAsk(
          QUESTIONS,
          ["hoursPerWeek", "knownSkills", "languages"],
          emptyAnswers(),
          false,
        ),
      ),
      ["goal", "background", "country"],
    );
  });

  test("always asks the goal and starting point when there is no answer for them", () => {
    assert.deepEqual(
      ids(
        questionsToAsk(
          QUESTIONS,
          ["goal", "background", "hoursPerWeek"],
          emptyAnswers(),
          false,
        ),
      ),
      ["goal", "background", "knownSkills", "country"],
    );
  });

  test("hides the goal when a saved answer exists (changing answers)", () => {
    const answers = { ...emptyAnswers(), goal: ["web"] };
    assert.deepEqual(ids(questionsToAsk(QUESTIONS, ["goal"], answers, false)), [
      "background",
      "knownSkills",
      "hoursPerWeek",
      "country",
    ]);
  });

  test("the edit link shows every question again", () => {
    assert.equal(
      questionsToAsk(QUESTIONS, ["hoursPerWeek"], emptyAnswers(), true).length,
      5,
    );
  });

  test("a country question without an 'anywhere' option is asked", () => {
    const qs = [question("country", ["c-ng", "c-gh"])];
    assert.deepEqual(
      ids(questionsToAsk(qs, ["country"], emptyAnswers(), false)),
      ["country"],
    );
  });
});

describe("hiddenQuestionIds", () => {
  test("lists only questions that are actually hidden", () => {
    assert.deepEqual(
      hiddenQuestionIds(
        QUESTIONS,
        ["goal", "hoursPerWeek", "knownSkills", "categories"],
        emptyAnswers(),
      ),
      ["knownSkills", "hoursPerWeek"],
    );
  });
});

describe("withPreferenceFallbacks", () => {
  test("completes the request for hidden questions and keeps the youth's own answers", () => {
    const answers = {
      ...emptyAnswers(),
      goal: ["web"],
      background: ["Degree"],
      country: ["c-ng"],
    };
    const filled = withPreferenceFallbacks(answers, QUESTIONS, [
      "hoursPerWeek",
      "knownSkills",
      "country",
    ]);
    assert.deepEqual(filled.hoursPerWeek, ["5"]);
    assert.deepEqual(filled.knownSkills, []);
    assert.deepEqual(filled.country, ["c-ng"]);
    assert.deepEqual(toProfileRequest(filled), {
      goalKey: "web",
      background: "Degree",
      knownSkillKeys: [],
      hoursPerWeek: 5,
      countryId: "c-ng",
    });
  });

  test("an empty hidden country becomes anywhere (null)", () => {
    const answers = {
      ...emptyAnswers(),
      goal: ["web"],
      background: ["Degree"],
      hoursPerWeek: ["2"],
    };
    const filled = withPreferenceFallbacks(answers, QUESTIONS, ["country"]);
    assert.equal(toProfileRequest(filled)?.countryId, null);
  });

  test("never invents a goal", () => {
    const filled = withPreferenceFallbacks(emptyAnswers(), QUESTIONS, ["goal"]);
    assert.deepEqual(filled.goal, []);
  });
});

describe("preferencesSummary", () => {
  test("names each preference once in plain words", () => {
    assert.equal(
      preferencesSummary([
        "hoursPerWeek",
        "languages",
        "knownSkills",
        "skills",
      ]),
      "time, languages, skills",
    );
  });

  test("is null without preferences", () => {
    assert.equal(preferencesSummary([]), null);
  });
});

describe("indexOfQuestion", () => {
  test("keeps the current question when the list grows", () => {
    assert.equal(indexOfQuestion(QUESTIONS, "country", 1), 4);
  });

  test("falls back to a valid index", () => {
    assert.equal(indexOfQuestion(QUESTIONS.slice(0, 2), undefined, 7), 1);
  });
});

describe("shouldRedirectToMyYoma", () => {
  const base: FirstVisitRedirectInput = {
    pathwayEnabled: true,
    sessionStatus: "authenticated",
    isAdminOrPartner: false,
    profileCompleted: true,
    settingsDialogPending: false,
    alreadyHandled: false,
    hasPathway: false,
  };

  test("sends a signed-in youth without a path to My Yoma", () => {
    assert.equal(shouldRedirectToMyYoma(base), true);
  });

  const cases: [string, Partial<FirstVisitRedirectInput>][] = [
    ["the switch is off", { pathwayEnabled: false }],
    ["signed out", { sessionStatus: "unauthenticated" }],
    ["the session is loading", { sessionStatus: "loading" }],
    ["an admin or partner", { isAdminOrPartner: true }],
    ["Yoma's profile wizard would open", { profileCompleted: false }],
    ["the profile is still loading", { profileCompleted: null }],
    ["Yoma's settings dialog would open", { settingsDialogPending: true }],
    ["already redirected or chose home", { alreadyHandled: true }],
    ["the youth has a path", { hasPathway: true }],
    ["the path check is loading or failed", { hasPathway: null }],
  ];
  for (const [name, change] of cases)
    test(`stays on the home page when ${name}`, () => {
      assert.equal(shouldRedirectToMyYoma({ ...base, ...change }), false);
    });
});

describe("isPreviewAllowed", () => {
  test("the demo build always shows the preview", () => {
    assert.equal(
      isPreviewAllowed({ production: true, demo: true, pathwayEnabled: false }),
      true,
    );
  });

  test("development shows it only with the switch on", () => {
    assert.equal(
      isPreviewAllowed({
        production: false,
        demo: false,
        pathwayEnabled: true,
      }),
      true,
    );
    assert.equal(
      isPreviewAllowed({
        production: false,
        demo: false,
        pathwayEnabled: false,
      }),
      false,
    );
  });

  test("production without the demo never shows it", () => {
    assert.equal(
      isPreviewAllowed({ production: true, demo: false, pathwayEnabled: true }),
      false,
    );
  });
});
