import assert from "node:assert/strict";
import { describe, test } from "node:test";
import {
  arrivingCopy,
  DEFAULT_JOURNEY_YOID,
  DEFAULT_PATHFINDER_URL,
  finalCopy,
  isHttpUrl,
  isJourneyComplete,
  isJourneyListing,
  JOURNEY_COMPLETE_LINE,
  JOURNEY_ITEMS,
  journeyItem,
  leavingCopy,
  listingUrl,
  parseJourneyParams,
  pathFinderLink,
  RECORDED_LINE,
  SAVE_HINT,
  threadLabel,
  threadSteps,
  toJourneyConfig,
  ziqiThreadSteps,
  ziqiVerdictLine,
} from "./journey";

const LISTING_ID = "c0a7e000-0000-4000-8000-000000000301";
const RETURN = `http://localhost:3000/opportunities/${LISTING_ID}`;

describe("JOURNEY_ITEMS", () => {
  test("the contract's four phases, names, ids and titles", () => {
    assert.deepEqual(
      JOURNEY_ITEMS.map((i) => [i.phase, i.app, i.key, i.name, i.id]),
      [
        [1, "Ziqi", "ziqi", "Saved Opportunity", "opp-job-001"],
        [2, "PathFinder", "pathfinder", "Learning Plan", "plan-001"],
        [3, "SkillLens", "skilllens", "Skill Credential", "cred-002"],
        [4, "YoCV", "yocv", "Job Application", "app-001"],
      ],
    );
    assert.equal(
      journeyItem(1).title,
      "Solar Installation Assistant (Trainee)",
    );
    assert.equal(
      journeyItem(2).title,
      "Plan step: complete a P2E course (Solar PV Installation Basics)",
    );
    assert.equal(
      journeyItem(3).title,
      "Solar PV Installation Basics: practical challenge passed",
    );
    assert.equal(
      journeyItem(4).title,
      "Application to Solar Installation Assistant (Trainee)",
    );
  });

  test("exact lines and label", () => {
    assert.equal(RECORDED_LINE, "✓ Recorded on your YoID (mock-up)");
    assert.equal(
      JOURNEY_COMPLETE_LINE,
      "✓ Journey complete: all four items are on your YoID (mock-up)",
    );
    assert.equal(
      SAVE_HINT,
      "Save this opportunity to hand it on to PathFinder.",
    );
    assert.equal(
      threadLabel("yoid-ng-7f3a0123"),
      "YoID · yoid-ng-7f3a0123 · mock-up",
    );
  });
});

describe("toJourneyConfig", () => {
  test("defaults for the optional settings", () => {
    assert.deepEqual(toJourneyConfig({ enabled: false }), {
      enabled: false,
      listingId: null,
      yoid: DEFAULT_JOURNEY_YOID,
      pathfinderUrl: DEFAULT_PATHFINDER_URL,
    });
    assert.equal(DEFAULT_JOURNEY_YOID, "yoid-ng-7f3a0123");
    assert.equal(DEFAULT_PATHFINDER_URL, "http://localhost:4173");
  });

  test("blank values fall back; set values are trimmed", () => {
    assert.deepEqual(
      toJourneyConfig({
        enabled: true,
        listingId: `  ${LISTING_ID} `,
        yoid: "  ",
        pathfinderUrl: "",
      }),
      {
        enabled: true,
        listingId: LISTING_ID,
        yoid: DEFAULT_JOURNEY_YOID,
        pathfinderUrl: DEFAULT_PATHFINDER_URL,
      },
    );
    assert.equal(
      toJourneyConfig({
        enabled: true,
        yoid: "yoid-x",
        pathfinderUrl: "https://pf.example",
      }).pathfinderUrl,
      "https://pf.example",
    );
  });
});

describe("isJourneyListing", () => {
  const on = toJourneyConfig({ enabled: true, listingId: LISTING_ID });

  test("only the configured listing, with the journey on", () => {
    assert.equal(isJourneyListing(on, LISTING_ID), true);
    assert.equal(isJourneyListing(on, LISTING_ID.toUpperCase()), true);
    assert.equal(
      isJourneyListing(on, "c0a7e000-0000-4000-8000-000000000302"),
      false,
    );
    assert.equal(isJourneyListing(on, ""), false);
    assert.equal(isJourneyListing(on, undefined), false);
  });

  test("off, or no listing id set: never", () => {
    assert.equal(
      isJourneyListing(
        toJourneyConfig({ enabled: false, listingId: LISTING_ID }),
        LISTING_ID,
      ),
      false,
    );
    assert.equal(
      isJourneyListing(toJourneyConfig({ enabled: true }), LISTING_ID),
      false,
    );
  });
});

describe("isHttpUrl", () => {
  test("http and https only", () => {
    assert.equal(isHttpUrl("http://localhost:3000/x"), true);
    assert.equal(isHttpUrl("https://yoma.world/opportunities/1"), true);
    assert.equal(isHttpUrl("javascript:alert(1)"), false);
    assert.equal(isHttpUrl("data:text/html,hi"), false);
    assert.equal(isHttpUrl("ftp://example.org"), false);
    assert.equal(isHttpUrl("/opportunities/1"), false);
    assert.equal(isHttpUrl("not a url"), false);
    assert.equal(isHttpUrl(""), false);
    assert.equal(isHttpUrl(null), false);
    assert.equal(isHttpUrl(undefined), false);
  });
});

describe("listingUrl", () => {
  test("origin + /opportunities/<id>", () => {
    assert.equal(listingUrl("http://localhost:3000", LISTING_ID), RETURN);
    assert.equal(listingUrl("http://localhost:3000/", LISTING_ID), RETURN);
    assert.equal(
      listingUrl("https://yoma.world", "a b"),
      "https://yoma.world/opportunities/a%20b",
    );
  });

  test("null without an http(s) origin or an id", () => {
    assert.equal(listingUrl("null", LISTING_ID), null);
    assert.equal(listingUrl("file://", LISTING_ID), null);
    assert.equal(listingUrl(undefined, LISTING_ID), null);
    assert.equal(listingUrl("http://localhost:3000", ""), null);
  });
});

describe("pathFinderLink", () => {
  test("contract link 1, parameters in order", () => {
    const href = pathFinderLink({
      pathfinderUrl: "http://localhost:4173",
      yoid: "yoid-ng-7f3a0123",
      returnUrl: RETURN,
    });
    assert.equal(
      href,
      "http://localhost:4173/?journey=demo&from=ziqi&yoid=yoid-ng-7f3a0123&listing=opp-job-001&goal=job&return=" +
        encodeURIComponent(RETURN),
    );
    const url = new URL(href!);
    assert.equal(url.searchParams.get("return"), RETURN);
  });

  test("a trailing slash or a path on PATHFINDER_URL", () => {
    assert.ok(
      pathFinderLink({
        pathfinderUrl: "http://localhost:4173/",
        yoid: "y",
        returnUrl: RETURN,
      })!.startsWith("http://localhost:4173/?journey=demo&"),
    );
    assert.ok(
      pathFinderLink({
        pathfinderUrl: "https://demo.example/pathfinder",
        yoid: "y",
        returnUrl: RETURN,
      })!.startsWith("https://demo.example/pathfinder/?journey=demo&"),
    );
  });

  test("a non-http(s) return URL is left out", () => {
    const href = pathFinderLink({
      pathfinderUrl: "http://localhost:4173",
      yoid: "y",
      returnUrl: "javascript:alert(1)",
    })!;
    assert.equal(new URL(href).searchParams.has("return"), false);
    assert.equal(
      new URL(
        pathFinderLink({
          pathfinderUrl: "http://localhost:4173",
          yoid: "y",
          returnUrl: null,
        })!,
      ).searchParams.has("return"),
      false,
    );
  });

  test("null for a non-http(s) PATHFINDER_URL", () => {
    assert.equal(
      pathFinderLink({
        pathfinderUrl: "javascript:alert(1)",
        yoid: "y",
        returnUrl: RETURN,
      }),
      null,
    );
    assert.equal(
      pathFinderLink({
        pathfinderUrl: "localhost:4173",
        yoid: "y",
        returnUrl: RETURN,
      }),
      null,
    );
  });
});

describe("parseJourneyParams", () => {
  test("the final link from YoCV", () => {
    const params = parseJourneyParams({
      opportunityId: LISTING_ID,
      journey: "demo",
      from: "yocv",
      yoid: "yoid-ng-7f3a0123",
      application: "app-001",
    });
    assert.equal(params.journey, true);
    assert.equal(params.from, "yocv");
    assert.equal(params.yoid, "yoid-ng-7f3a0123");
    assert.equal(params.application, "app-001");
    assert.equal(params.returnUrl, null);
    assert.equal(isJourneyComplete(params), true);
  });

  test("every contract parameter; arrays take the first value", () => {
    const params = parseJourneyParams({
      journey: ["demo", "x"],
      from: "skilllens",
      yoid: "y",
      listing: "opp-job-001",
      goal: "job",
      plan: "plan-001",
      course: "opp-course-001",
      credential: "cred-002",
      return: RETURN,
    });
    assert.deepEqual(params, {
      journey: true,
      from: "skilllens",
      yoid: "y",
      listing: "opp-job-001",
      goal: "job",
      plan: "plan-001",
      course: "opp-course-001",
      credential: "cred-002",
      application: null,
      returnUrl: RETURN,
    });
  });

  test("a non-http(s) return is ignored", () => {
    assert.equal(
      parseJourneyParams({ journey: "demo", return: "javascript:alert(1)" })
        .returnUrl,
      null,
    );
    assert.equal(
      parseJourneyParams({ journey: "demo", return: "/opportunities/1" })
        .returnUrl,
      null,
    );
  });

  test("without journey=demo, or not from YoCV with app-001: not complete", () => {
    assert.equal(
      isJourneyComplete(
        parseJourneyParams({ from: "yocv", application: "app-001" }),
      ),
      false,
    );
    assert.equal(
      isJourneyComplete(
        parseJourneyParams({
          journey: "Demo",
          from: "yocv",
          application: "app-001",
        }),
      ),
      false,
    );
    assert.equal(
      isJourneyComplete(
        parseJourneyParams({
          journey: "demo",
          from: "skilllens",
          application: "app-001",
        }),
      ),
      false,
    );
    assert.equal(
      isJourneyComplete(
        parseJourneyParams({
          journey: "demo",
          from: "yocv",
          application: "app-002",
        }),
      ),
      false,
    );
    assert.equal(parseJourneyParams({}).journey, false);
  });
});

describe("threadSteps", () => {
  const states = (steps: { state: string }[]) => steps.map((s) => s.state);

  test("the contract table, for each phase", () => {
    assert.deepEqual(states(threadSteps({ phase: 1, done: false })), [
      "in progress",
      "next",
      "next",
      "next",
    ]);
    assert.deepEqual(states(threadSteps({ phase: 1, done: true })), [
      "recorded",
      "next",
      "next",
      "next",
    ]);
    assert.deepEqual(states(threadSteps({ phase: 3, done: false })), [
      "recorded",
      "recorded",
      "in progress",
      "next",
    ]);
    assert.deepEqual(states(threadSteps({ phase: 4, done: true })), [
      "recorded",
      "recorded",
      "recorded",
      "recorded",
    ]);
  });

  test("step names in order", () => {
    assert.deepEqual(
      threadSteps({ phase: 1, done: false }).map((s) => s.name),
      [
        "Saved Opportunity",
        "Learning Plan",
        "Skill Credential",
        "Job Application",
      ],
    );
  });

  test("Ziqi: saved, and the last screen", () => {
    assert.deepEqual(
      states(ziqiThreadSteps({ saved: false, complete: false })),
      ["in progress", "next", "next", "next"],
    );
    assert.deepEqual(
      states(ziqiThreadSteps({ saved: true, complete: false })),
      ["recorded", "next", "next", "next"],
    );
    assert.deepEqual(
      states(ziqiThreadSteps({ saved: false, complete: true })),
      ["recorded", "recorded", "recorded", "recorded"],
    );
  });
});

describe("handoff copy", () => {
  test("Ziqi leaving: exact wording", () => {
    assert.deepEqual(leavingCopy(1), {
      variant: "leaving",
      eyebrow: "Handing on to PathFinder · Phase 2",
      title: "Saved Opportunity",
      itemTitle: "Solar Installation Assistant (Trainee)",
      button: "Continue to PathFinder",
      line: "✓ Recorded on your YoID (mock-up)",
    });
    assert.equal(leavingCopy(3).button, "Continue to YoCV");
    assert.equal(leavingCopy(2).eyebrow, "Handing on to SkillLens · Phase 3");
  });

  test("Ziqi final: exact wording", () => {
    assert.deepEqual(finalCopy(), {
      variant: "final",
      eyebrow: "From YoCV · Phase 4",
      title: "Job Application received",
      itemTitle: "Application to Solar Installation Assistant (Trainee)",
      button: null,
      line: "✓ Journey complete: all four items are on your YoID (mock-up)",
    });
  });

  test("arriving: the previous phase's item", () => {
    assert.deepEqual(arrivingCopy(2), {
      variant: "arriving",
      eyebrow: "From Ziqi · Phase 1",
      title: "Saved Opportunity",
      itemTitle: "Solar Installation Assistant (Trainee)",
      button: null,
      line: "✓ Recorded on your YoID (mock-up)",
    });
    assert.equal(arrivingCopy(4).eyebrow, "From SkillLens · Phase 3");
  });
});

describe("ziqiVerdictLine", () => {
  test("verified with a grade", () => {
    assert.equal(
      ziqiVerdictLine({ status: "Verified", grade: "A" }),
      "Checked by Ziqi: Verified · Grade A",
    );
  });

  test("verified without a grade, or Ungraded: no grade is shown", () => {
    assert.equal(
      ziqiVerdictLine({ status: "Verified", grade: null }),
      "Checked by Ziqi: Verified",
    );
    assert.equal(
      ziqiVerdictLine({ status: "Verified", grade: "Ungraded" }),
      "Checked by Ziqi: Verified",
    );
  });

  test("not verified never shows a grade", () => {
    assert.equal(
      ziqiVerdictLine({ status: "NeedsReview", grade: "B" }),
      "Checked by Ziqi: Not yet verified",
    );
    assert.equal(
      ziqiVerdictLine({ status: "NotVerified", grade: null }),
      "Checked by Ziqi: Not yet verified",
    );
  });

  test("no card: no line", () => {
    assert.equal(ziqiVerdictLine(null), null);
    assert.equal(ziqiVerdictLine(undefined), null);
  });
});
