import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type { CourseFeedbackSummary } from "~/api/models/courseQuality";
import {
  SUMMARY_TOP_TAGS,
  toFeedbackSummaryView,
  toSplit,
} from "./courseFeedbackSummaryModel";

const SUMMARY: CourseFeedbackSummary = {
  opportunityId: "opp-1",
  responses: 10,
  completed: 6,
  averageRating: 4.25,
  adjustedRating: 3.9,
  wouldRecommendYes: 7,
  wouldRecommendNo: 3,
  matchedDescriptionYes: 0,
  matchedDescriptionNo: 0,
  good: [
    { key: "a", label: "Clear", count: 3 },
    { key: "b", label: "Practical", count: 8 },
    { key: "c", label: "Short", count: 0 },
    { key: "d", label: "Fun", count: 1 },
    { key: "e", label: "Free", count: 2 },
    { key: "f", label: "Certificate", count: 2 },
    { key: "g", label: "Mobile", count: 1 },
  ],
  bad: [{ key: "x", label: "Too long", count: 12 }],
  recentComments: ["Great course", "  ", "Too many ads"],
};

describe("toFeedbackSummaryView", () => {
  test("empty state when there is no summary or no answers", () => {
    assert.equal(toFeedbackSummaryView(null, true), null);
    assert.equal(
      toFeedbackSummaryView({ ...SUMMARY, responses: 0 }, true),
      null,
    );
  });

  test("ratings, finished and splits", () => {
    const view = toFeedbackSummaryView(SUMMARY, false)!;
    assert.equal(view.responses, 10);
    assert.equal(view.completed, 6);
    assert.equal(view.averageRating, "4.3");
    assert.equal(view.adjustedRating, "3.9");
    assert.deepEqual(view.recommend, { yes: 7, no: 3, yesPercent: 70 });
    assert.deepEqual(view.matched, { yes: 0, no: 0, yesPercent: null });
  });

  test("top tags, most picked first, as a share of answers", () => {
    const view = toFeedbackSummaryView(SUMMARY, false)!;
    assert.equal(view.good.length, SUMMARY_TOP_TAGS);
    assert.deepEqual(
      view.good.map((t) => t.label),
      ["Practical", "Clear", "Certificate", "Free", "Fun"],
    );
    assert.equal(view.good[0]!.percent, 80);
    assert.equal(
      view.good.some((t) => t.count === 0),
      false,
    );
    assert.equal(view.bad[0]!.percent, 100);
  });

  test("comments only when asked for, blanks dropped", () => {
    assert.deepEqual(toFeedbackSummaryView(SUMMARY, false)!.comments, []);
    assert.deepEqual(toFeedbackSummaryView(SUMMARY, true)!.comments, [
      "Great course",
      "Too many ads",
    ]);
  });

  test("a missing rating reads as none", () => {
    const view = toFeedbackSummaryView(
      { ...SUMMARY, averageRating: null, adjustedRating: null },
      false,
    )!;
    assert.equal(view.averageRating, null);
    assert.equal(view.adjustedRating, null);
  });
});

describe("toSplit", () => {
  test("rounds the yes share", () => {
    assert.equal(toSplit(1, 2).yesPercent, 33);
    assert.equal(toSplit(0, 4).yesPercent, 0);
  });
});
