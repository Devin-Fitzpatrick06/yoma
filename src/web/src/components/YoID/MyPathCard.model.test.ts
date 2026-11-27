import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type { MyPathSummary } from "~/api/models/pathway";
import {
  opportunityHref,
  percentOf,
  ringStroke,
  toPathProgress,
  toStageProgress,
} from "./MyPathCard.model";

const SUMMARY: MyPathSummary = {
  goalLabel: "Digital marketing assistant",
  totalSteps: 11,
  completedSteps: 4,
  nextStep: { opportunityId: "opp-5", title: "Social media basics" },
  stages: [
    { name: "Foundations", completed: 3, total: 3 },
    { name: "Build skills", completed: 1, total: 3 },
    { name: "Empty", completed: 0, total: 0 },
  ],
};

describe("percentOf", () => {
  test("rounds and clamps", () => {
    assert.equal(percentOf(4, 11), 36);
    assert.equal(percentOf(12, 11), 100);
    assert.equal(percentOf(-1, 11), 0);
    assert.equal(percentOf(3, 0), 0);
    assert.equal(percentOf(Number.NaN, 4), 0);
  });
});

describe("toPathProgress", () => {
  test("labels steps done", () => {
    assert.deepEqual(toPathProgress(SUMMARY), {
      completed: 4,
      total: 11,
      percent: 36,
      label: "4 of 11 steps done",
    });
  });

  test("singular step, and completed never above total", () => {
    const progress = toPathProgress({
      ...SUMMARY,
      totalSteps: 1,
      completedSteps: 3,
    });
    assert.equal(progress.completed, 1);
    assert.equal(progress.percent, 100);
    assert.equal(progress.label, "1 of 1 step done");
  });
});

describe("toStageProgress", () => {
  test("per-stage percent, done and a readable label", () => {
    const stages = toStageProgress(SUMMARY.stages);
    assert.equal(stages.length, 3);
    assert.deepEqual(
      stages.map((s) => [s.percent, s.done]),
      [
        [100, true],
        [33, false],
        [0, false],
      ],
    );
    assert.equal(stages[1]!.label, "Build skills: 1 of 3 done");
  });

  test("no stages", () => {
    assert.deepEqual(toStageProgress(null), []);
  });
});

describe("ringStroke", () => {
  test("offset shrinks as the ring fills", () => {
    const empty = ringStroke(0, 10);
    const half = ringStroke(50, 10);
    const full = ringStroke(150, 10);
    assert.equal(empty.offset, empty.circumference);
    assert.ok(Math.abs(half.offset - half.circumference / 2) < 1e-9);
    assert.equal(full.offset, 0);
  });
});

test("opportunityHref encodes the id", () => {
  assert.equal(opportunityHref("a b"), "/opportunities/a%20b");
});
