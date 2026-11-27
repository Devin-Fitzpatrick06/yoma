import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type { PartnerBadge } from "../models/pathway";
import {
  formatReviewDue,
  isEarnedPartnerBadge,
  partnerBadgeIds,
  partnerBadgeMap,
  partnerBadgesQueryKey,
  reviewDueLine,
} from "./partnerBadges.helpers";

describe("batched partner badges", () => {
  test("ids are unique, trimmed, lowercased and sorted", () => {
    assert.deepEqual(partnerBadgeIds(["B", "a", null, "", " b ", undefined]), [
      "a",
      "b",
    ]);
  });
  test("the query key is the same whatever the order of the cards", () => {
    assert.deepEqual(
      partnerBadgesQueryKey(["x", "y", "x"]),
      partnerBadgesQueryKey(["y", "x"]),
    );
  });
  test("only earned levels show a chip", () => {
    assert.equal(isEarnedPartnerBadge("Platinum"), true);
    assert.equal(isEarnedPartnerBadge("Bronze"), true);
    assert.equal(isEarnedPartnerBadge("None"), false);
    assert.equal(isEarnedPartnerBadge("NotEnoughGraded"), false);
    assert.equal(isEarnedPartnerBadge(null), false);
  });
  test("the map looks organisations up case-insensitively", () => {
    const badge = { organizationId: "ABC", level: "Gold" } as PartnerBadge;
    assert.equal(partnerBadgeMap([badge]).get("abc"), badge);
    assert.equal(partnerBadgeMap(null).size, 0);
  });
});

describe("review date", () => {
  test("the next quarterly review in words", () => {
    assert.equal(formatReviewDue("2027-01-01T00:00:00Z"), "1 Jan 2027");
    assert.equal(
      reviewDueLine("2027-04-01"),
      "Reviewed each quarter · next review 1 Apr 2027",
    );
  });
  test("a missing or bad date still says how often", () => {
    assert.equal(formatReviewDue("not a date"), null);
    assert.equal(reviewDueLine(null), "Reviewed each quarter");
  });
});
