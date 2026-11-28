import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type { CourseQualityAIUsage } from "~/api/models/courseQuality";
import {
  AI_USAGE_LOCAL_FIXTURE,
  AI_USAGE_REMOTE_PRICED_FIXTURE,
  AI_USAGE_REMOTE_UNPRICED_FIXTURE,
} from "./AIUsage.fixtures";
import {
  COST_NOT_CONFIGURED_TEXT,
  describeModel,
  formatDuration,
  formatElapsed,
  formatRunCost,
  formatRunLine,
  formatTokenPair,
  formatTokens,
  formatTotalCost,
  formatUsd,
  LOCAL_COST_TEXT,
  OUTCOME_EXPLANATIONS,
} from "./AIUsage.helpers";

describe("formatTokens", () => {
  test("thousands separators, whole and never negative", () => {
    assert.equal(formatTokens(3214), "3,214");
    assert.equal(formatTokens(1_234_567.6), "1,234,568");
    assert.equal(formatTokens(-5), "0");
    assert.equal(formatTokens(null), "0");
    assert.equal(formatTokens(Number.NaN), "0");
  });

  test("the pair reads in / out", () => {
    assert.equal(formatTokenPair(3214, 412), "3,214 in / 412 out tokens");
  });
});

describe("cost", () => {
  test("four decimals under a dollar, two from a dollar up", () => {
    assert.equal(formatUsd(0.0412), "$0.0412");
    assert.equal(formatUsd(0.00004), "$0.0000");
    assert.equal(formatUsd(12.4), "$12.40");
    assert.equal(formatUsd(0), "$0.00");
  });

  test("a local run is free whatever cost came along", () => {
    assert.equal(
      formatRunCost({ isLocal: true, costUsd: null }),
      LOCAL_COST_TEXT,
    );
    assert.equal(
      formatRunCost({ isLocal: true, costUsd: 0 }),
      "$0.00 (local, free)",
    );
  });

  test("a remote run without a price is never guessed", () => {
    assert.equal(
      formatRunCost({ isLocal: false, costUsd: null }),
      COST_NOT_CONFIGURED_TEXT,
    );
    assert.equal(formatRunCost({ isLocal: false, costUsd: 0.0412 }), "$0.0412");
  });

  test("the panel total is free for a local model with nothing billed", () => {
    assert.equal(
      formatTotalCost({ isLocal: true, costUsd: 0 }),
      LOCAL_COST_TEXT,
    );
    assert.equal(formatTotalCost({ isLocal: false, costUsd: 0 }), "$0.00");
    assert.equal(formatTotalCost({ isLocal: false, costUsd: 3.5 }), "$3.50");
  });
});

describe("formatDuration", () => {
  test("seconds, then minutes and seconds", () => {
    assert.equal(formatDuration(420), "<1 s");
    assert.equal(formatDuration(42_000), "42 s");
    assert.equal(formatDuration(59_400), "59 s");
    assert.equal(formatDuration(125_000), "2 min 5 s");
    assert.equal(formatDuration(180_000), "3 min");
  });

  test("a missing or broken value is a dash", () => {
    assert.equal(formatDuration(null), "–");
    assert.equal(formatDuration(-1), "–");
  });

  test("elapsed seconds add minutes once past a minute", () => {
    assert.equal(formatElapsed(0), "0 s");
    assert.equal(formatElapsed(37.9), "37 s");
    assert.equal(formatElapsed(75), "75 s (1 min 15 s)");
  });
});

describe("describeModel", () => {
  test("names the host when there is one, else the provider", () => {
    assert.equal(
      describeModel("OpenAICompatible", "qwen2.5:3b", "localhost:11434"),
      "qwen2.5:3b on localhost:11434",
    );
    assert.equal(
      describeModel("Anthropic", "claude-sonnet-4-5", null),
      "claude-sonnet-4-5 via Anthropic",
    );
    assert.equal(describeModel("None", null, null), "model not set");
  });
});

describe("formatRunLine", () => {
  test("a local run reads as the reviewer sees it", () => {
    assert.equal(
      formatRunLine(AI_USAGE_LOCAL_FIXTURE),
      "Local model · qwen2.5:3b on localhost:11434 · 3,214 in / 412 out tokens · $0.00 (local, free) · 42 s · Answered",
    );
  });

  test("a remote run shows its price, or says none is configured", () => {
    assert.equal(
      formatRunLine(AI_USAGE_REMOTE_PRICED_FIXTURE),
      "Remote model · claude-sonnet-4-5 via Anthropic · 9,120 in / 925 out tokens · $0.0412 · 6 s · Answered",
    );
    assert.match(
      formatRunLine(AI_USAGE_REMOTE_UNPRICED_FIXTURE),
      /cost not configured · 2 min 8 s · Invalid$/,
    );
  });
});

describe("outcome wording", () => {
  const outcomes: CourseQualityAIUsage["outcome"][] = [
    "Declined",
    "Failed",
    "Invalid",
  ];

  test("every outcome where the AI did not count says the rule-based result stands", () => {
    for (const outcome of outcomes)
      assert.match(OUTCOME_EXPLANATIONS[outcome], /rule-based result stands/);
  });

  test("Invalid says the reply was not in the required format", () => {
    assert.equal(
      OUTCOME_EXPLANATIONS.Invalid,
      "The model did not answer in the required format, so the rule-based result stands.",
    );
  });

  test("Answered says the AI can only lower a result", () => {
    assert.match(OUTCOME_EXPLANATIONS.Answered, /only lower/);
  });
});
