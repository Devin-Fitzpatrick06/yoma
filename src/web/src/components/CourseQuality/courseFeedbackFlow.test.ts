import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  EMPTY_DRAFT,
  FEEDBACK_CHECK_INTERVAL_MS,
  FEEDBACK_COMMENT_MAX,
  INITIAL_FLOW_STATE,
  buildFeedbackSubmission,
  createThrottle,
  feedbackFlowReducer,
  isAnotherModalOpen,
  isFeedbackPromptRoute,
  type FeedbackFlowAction,
  type FeedbackFlowState,
} from "./courseFeedbackFlow";

const run = (...actions: FeedbackFlowAction[]): FeedbackFlowState =>
  actions.reduce(feedbackFlowReducer, INITIAL_FLOW_STATE);

const EMPTY_SURVEY = {
  rating: null,
  completed: null,
  wouldRecommend: null,
  matchedDescription: null,
  good: [],
  bad: [],
  comment: null,
};

describe("the prompt flow", () => {
  test("Yes → step 1 → step 2 → submit sends the whole survey", () => {
    const state = run(
      { type: "yes" },
      { type: "setRating", rating: 4 },
      { type: "setAnswer", field: "completed", value: true },
      { type: "setAnswer", field: "wouldRecommend", value: false },
      { type: "next" },
      { type: "toggleTag", list: "good", key: "clear" },
      { type: "toggleTag", list: "good", key: "practical" },
      { type: "toggleTag", list: "good", key: "clear" },
      { type: "toggleTag", list: "bad", key: "too_long" },
      { type: "setAnswer", field: "matchedDescription", value: true },
      { type: "setComment", comment: "  Loved the videos.  " },
    );
    assert.equal(state.step, "tags");
    assert.equal(state.answer, "Taken");

    assert.deepEqual(buildFeedbackSubmission("Taken", state.draft), {
      answer: "Taken",
      rating: 4,
      completed: true,
      wouldRecommend: false,
      matchedDescription: true,
      good: ["practical"],
      bad: ["too_long"],
      comment: "Loved the videos.",
    });

    assert.equal(
      feedbackFlowReducer(state, { type: "finished", answer: "Taken" }).step,
      "thanks",
    );
  });

  test("skipping at step 1 still records Taken with what was filled", () => {
    const state = run({ type: "yes" }, { type: "setRating", rating: 2 });
    assert.equal(state.step, "rate");
    assert.deepEqual(buildFeedbackSubmission("Taken", state.draft), {
      ...EMPTY_SURVEY,
      answer: "Taken",
      rating: 2,
    });
  });

  test("Not yet, No and dismiss send the answer with an empty survey", () => {
    const filled = run(
      { type: "yes" },
      { type: "setRating", rating: 5 },
      { type: "setComment", comment: "hi" },
    ).draft;
    for (const answer of ["NotYet", "NotTaken", "Dismissed"] as const) {
      assert.deepEqual(buildFeedbackSubmission(answer, filled), {
        ...EMPTY_SURVEY,
        answer,
      });
      const done = run({ type: "finished", answer });
      assert.equal(done.step, "thanks");
      assert.equal(done.answer, answer);
    }
  });

  test("a rating is a whole star from 1 to 5", () => {
    assert.equal(run({ type: "setRating", rating: 9 }).draft.rating, 5);
    assert.equal(run({ type: "setRating", rating: 0 }).draft.rating, 1);
    assert.equal(run({ type: "setRating", rating: 3.4 }).draft.rating, 3);
  });

  test("the comment is capped at 500 characters and blank becomes null", () => {
    const long = run({ type: "setComment", comment: "a".repeat(700) });
    assert.equal(long.draft.comment.length, FEEDBACK_COMMENT_MAX);
    assert.equal(
      buildFeedbackSubmission("Taken", { ...EMPTY_DRAFT, comment: "   " })
        .comment,
      null,
    );
  });

  test("next and back move only between the two survey steps", () => {
    assert.equal(run({ type: "next" }).step, "ask");
    assert.equal(
      run({ type: "yes" }, { type: "next" }, { type: "back" }).step,
      "rate",
    );
    assert.equal(
      run({ type: "yes" }, { type: "next" }, { type: "next" }).step,
      "tags",
    );
    assert.deepEqual(
      run({ type: "yes" }, { type: "reset" }),
      INITIAL_FLOW_STATE,
    );
  });
});

describe("createThrottle", () => {
  test("at most one check a minute", () => {
    const throttle = createThrottle();
    const t0 = 1_000_000;
    assert.equal(throttle.tryAcquire(t0), true);
    assert.equal(throttle.tryAcquire(t0 + 1), false);
    assert.equal(
      throttle.tryAcquire(t0 + FEEDBACK_CHECK_INTERVAL_MS - 1),
      false,
    );
    assert.equal(throttle.tryAcquire(t0 + FEEDBACK_CHECK_INTERVAL_MS), true);
    assert.equal(
      throttle.tryAcquire(t0 + FEEDBACK_CHECK_INTERVAL_MS + 5),
      false,
    );
  });

  test("reset opens the gate again", () => {
    const throttle = createThrottle(10_000);
    assert.equal(throttle.tryAcquire(0), true);
    throttle.reset();
    assert.equal(throttle.tryAcquire(1), true);
  });
});

describe("isFeedbackPromptRoute", () => {
  test("hidden on admin, organisation and auth routes", () => {
    for (const path of [
      "/admin",
      "/admin/course-quality",
      "/admin/course-quality/[id]",
      "/organisations",
      "/organisations/[id]/course-quality",
      "/organisations/[id]/opportunities",
      "/auth/signin",
    ])
      assert.equal(isFeedbackPromptRoute(path), false, path);
  });

  test("shown on learner pages", () => {
    for (const path of [
      "/",
      "/opportunities",
      "/opportunities/[id]",
      "/yoid",
      "/administrator-guide",
      "/organisationsx",
    ])
      assert.equal(isFeedbackPromptRoute(path), true, path);
  });
});

describe("isAnotherModalOpen", () => {
  test("true when the document has an open dialog", () => {
    assert.equal(
      isAnotherModalOpen({ querySelector: () => ({}) as Element }),
      true,
    );
    assert.equal(isAnotherModalOpen({ querySelector: () => null }), false);
    assert.equal(isAnotherModalOpen(null), false);
    assert.equal(
      isAnotherModalOpen({
        querySelector: () => {
          throw new Error("bad selector");
        },
      }),
      false,
    );
  });
});
