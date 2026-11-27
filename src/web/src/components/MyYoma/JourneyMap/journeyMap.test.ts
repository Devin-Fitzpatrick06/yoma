import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type {
  PathwayCourse,
  PathwayStage,
  PathwayStepStatus,
  PathwayStepType,
} from "~/api/models/pathway";
import {
  celebrationsBetween,
  dropOffLine,
  hoursLabel,
  isStageComplete,
  journeySteps,
  stageKind,
  topNextSteps,
  trailFraction,
  trailPath,
  visiblePartnerBadge,
  youAreHereIndex,
} from "./journeyMap";

const step = (
  id: string,
  status: PathwayStepStatus = "NotStarted",
  type: PathwayStepType = "Learning",
): PathwayCourse =>
  ({ opportunityId: id, status, type }) as unknown as PathwayCourse;

const stage = (order: number, courses: PathwayCourse[]): PathwayStage => ({
  order,
  name: `Stage ${order}`,
  why: "",
  leadsTo: null,
  shortened: false,
  courses,
});

describe("journeySteps", () => {
  test("orders stages by `order` and numbers steps across the path", () => {
    const steps = journeySteps({
      stages: [
        stage(2, [step("c"), step("d")]),
        stage(1, [step("a"), step("b")]),
      ],
    });
    assert.deepEqual(
      steps.map((s) => [s.course.opportunityId, s.number, s.stageIndex]),
      [
        ["a", 1, 0],
        ["b", 2, 0],
        ["c", 3, 1],
        ["d", 4, 1],
      ],
    );
  });

  test("an empty path has no steps", () => {
    assert.deepEqual(journeySteps({ stages: [] }), []);
  });
});

describe("youAreHereIndex", () => {
  test("is the first step not completed, in progress or not", () => {
    const steps = journeySteps({
      stages: [
        stage(1, [step("a", "Completed"), step("b", "InProgress")]),
        stage(2, [step("c")]),
      ],
    });
    assert.equal(youAreHereIndex(steps), 1);
  });

  test("skips nothing out of order: a later completed step does not move the marker", () => {
    const steps = journeySteps({
      stages: [stage(1, [step("a"), step("b", "Completed")])],
    });
    assert.equal(youAreHereIndex(steps), 0);
  });

  test("is past the last step when everything is done", () => {
    const steps = journeySteps({
      stages: [stage(1, [step("a", "Completed"), step("b", "Completed")])],
    });
    assert.equal(youAreHereIndex(steps), 2);
  });
});

describe("isStageComplete", () => {
  test("needs every step completed", () => {
    assert.equal(isStageComplete(stage(1, [step("a", "Completed")])), true);
    assert.equal(
      isStageComplete(
        stage(1, [step("a", "Completed"), step("b", "InProgress")]),
      ),
      false,
    );
  });

  test("an empty stage is not complete", () => {
    assert.equal(isStageComplete(stage(1, [])), false);
  });
});

describe("celebrationsBetween", () => {
  test("nothing on the first render", () => {
    assert.deepEqual(
      celebrationsBetween(null, {
        stages: [stage(1, [step("a", "Completed")])],
      }),
      { steps: [], stages: [] },
    );
  });

  test("a step that became completed, and the stage it finished", () => {
    const before = {
      stages: [
        stage(1, [step("a", "Completed"), step("b", "InProgress")]),
        stage(2, [step("c"), step("d")]),
      ],
    };
    const after = {
      stages: [
        stage(1, [step("a", "Completed"), step("b", "Completed")]),
        stage(2, [step("c", "Completed"), step("d")]),
      ],
    };
    assert.deepEqual(celebrationsBetween(before, after), {
      steps: ["b", "c"],
      stages: [1],
    });
  });

  test("a swapped-in step that is already completed does not celebrate", () => {
    const before = { stages: [stage(1, [step("a"), step("b")])] };
    const after = { stages: [stage(1, [step("x", "Completed"), step("b")])] };
    assert.deepEqual(celebrationsBetween(before, after), {
      steps: [],
      stages: [],
    });
  });

  test("an unchanged path celebrates nothing", () => {
    const path = { stages: [stage(1, [step("a", "Completed")])] };
    assert.deepEqual(celebrationsBetween(path, path), {
      steps: [],
      stages: [],
    });
  });
});

describe("stageKind", () => {
  test("practise when a stage holds an impact action or task", () => {
    assert.equal(
      stageKind(
        stage(2, [step("a"), step("b", "NotStarted", "ImpactAction")]),
        1,
        3,
      ),
      "Practise",
    );
    assert.equal(
      stageKind(stage(2, [step("a", "NotStarted", "Task")]), 0, 1),
      "Practise",
    );
  });

  test("prove for the last stage of a longer path, learn otherwise", () => {
    assert.equal(stageKind(stage(3, [step("a")]), 2, 3), "Prove");
    assert.equal(stageKind(stage(1, [step("a")]), 0, 3), "Learn");
    assert.equal(stageKind(stage(2, [step("a")]), 1, 2), "Learn");
  });
});

describe("card helpers", () => {
  test("only earned partner levels show", () => {
    assert.equal(visiblePartnerBadge("Platinum"), "Platinum");
    assert.equal(visiblePartnerBadge("Bronze"), "Bronze");
    assert.equal(visiblePartnerBadge("None"), null);
    assert.equal(visiblePartnerBadge("NotEnoughGraded"), null);
    assert.equal(visiblePartnerBadge(null), null);
  });

  test("next steps: most common first, empty ones dropped, at most two", () => {
    const next = topNextSteps([
      { opportunityId: "a", title: "A", share: 0.2, count: 30 },
      { opportunityId: "b", title: "B", share: 0.5, count: 60 },
      { opportunityId: "c", title: "C", share: 0.4, count: 0 },
      { opportunityId: "d", title: "D", share: 0.3, count: 25 },
    ]);
    assert.deepEqual(
      next.map((n) => n.opportunityId),
      ["b", "d"],
    );
  });

  test("drop-off slot: labelled once, never a number", () => {
    assert.equal(
      dropOffLine("Collecting from the pilot"),
      "Drop-off: collecting from the pilot",
    );
    assert.equal(
      dropOffLine("Drop-off: collecting from the pilot"),
      "Drop-off: collecting from the pilot",
    );
    assert.equal(dropOffLine(null), "Drop-off: collecting from the pilot");
  });

  test("hours", () => {
    assert.equal(hoursLabel(6), "6 h");
    assert.equal(hoursLabel(null), "Length not listed");
  });
});

describe("the trail", () => {
  test("passes through every point", () => {
    const d = trailPath([
      { x: 0, y: 0 },
      { x: 0, y: 100 },
      { x: 0, y: 200 },
    ]);
    assert.match(d, /^M 0 0 Q -?[\d.]+ 50 0 100 Q -?[\d.]+ 150 0 200$/);
  });

  test("an empty trail draws nothing", () => {
    assert.equal(trailPath([]), "");
  });

  test("fraction along the trail grows with the index and is clamped", () => {
    const points = [
      { x: 0, y: 0 },
      { x: 100, y: 0 },
      { x: 200, y: 0 },
      { x: 300, y: 0 },
    ];
    assert.equal(trailFraction(points, 0), 0);
    const one = trailFraction(points, 1);
    assert.ok(Math.abs(one - 1 / 3) < 0.001, `${one}`);
    assert.equal(trailFraction(points, 3), 1);
    assert.equal(trailFraction(points, 10), 1);
    assert.equal(trailFraction([{ x: 0, y: 0 }], 0), 0);
  });
});
