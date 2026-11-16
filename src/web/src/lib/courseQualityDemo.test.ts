import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  DEFAULT_DEMO_ORG_ID,
  demoSideOf,
  demoSides,
} from "./courseQualityDemo";

describe("demo sides", () => {
  test("routes map to their side", () => {
    assert.equal(demoSideOf("/opportunities"), "learner");
    assert.equal(demoSideOf("/opportunities/[opportunityId]"), "learner");
    assert.equal(demoSideOf("/opportunities/abc?x=1"), "learner");
    assert.equal(demoSideOf("/admin/course-quality"), "reviewer");
    assert.equal(demoSideOf("/admin/course-quality/review"), "reviewer");
    assert.equal(demoSideOf("/admin/course-quality/rubric"), "reviewer");
    assert.equal(demoSideOf("/organisations/[id]/course-quality"), "partner");
    assert.equal(
      demoSideOf(`/organisations/${DEFAULT_DEMO_ORG_ID}/course-quality`),
      "partner",
    );
  });

  test("other routes have no side", () => {
    assert.equal(demoSideOf("/"), null);
    assert.equal(demoSideOf("/admin/opportunities"), null);
    assert.equal(demoSideOf("/organisations/[id]/opportunities"), null);
    assert.equal(demoSideOf("/opportunitiesx"), null);
  });

  test("the partner link uses the configured organisation", () => {
    assert.equal(
      demoSides()[2]!.href,
      `/organisations/${DEFAULT_DEMO_ORG_ID}/course-quality`,
    );
    assert.equal(
      demoSides("org-1")[2]!.href,
      "/organisations/org-1/course-quality",
    );
    assert.deepEqual(
      demoSides().map((s) => s.label),
      ["Learner", "Reviewer", "Partner"],
    );
  });
});
