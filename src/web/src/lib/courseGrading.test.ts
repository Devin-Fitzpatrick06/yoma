import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type {
  CourseGradeFactor,
  CourseGradeFactorRow,
  CourseGradeRubric,
  CourseQualityStatus,
} from "~/api/models/courseQuality";
import {
  bandShares,
  GRADE_FACTORS,
  gradeDistribution,
  normaliseWeights,
  isWaitingForReviewCap,
  NEEDS_REVIEW_UNGRADED_REASON,
  regradeCatalogue,
  roundHalfAway,
  validateRubric,
  weightedScore,
  withBandShare,
} from "./courseGrading";

const WEIGHTS: Record<CourseGradeFactor, number> = {
  LearnerFeedback: 20,
  SyllabusAvailable: 18,
  ProviderReliability: 15,
  Comprehensiveness: 12,
  CourseLength: 10,
  Completion: 10,
  ListingIntegrity: 8,
  Accessibility: 7,
};

const rubric = (
  overrides: Partial<CourseGradeRubric> = {},
): CourseGradeRubric => ({
  version: "test",
  factors: Object.fromEntries(
    GRADE_FACTORS.map((factor) => [
      factor,
      { weight: WEIGHTS[factor], neutralScore: 50, parameters: {} },
    ]),
  ) as CourseGradeRubric["factors"],
  bands: [
    { grade: "A", label: "Top pick", topPercent: 15, minScore: 65 },
    { grade: "B", label: "Strong", topPercent: 40, minScore: 0 },
    { grade: "C", label: "Good", topPercent: 75, minScore: 0 },
    { grade: "D", label: "Basic", topPercent: 100, minScore: 0 },
  ],
  needsReviewMaxGrade: "B",
  aMinSyllabusScore: 50,
  aMinFeedbackScore: 60,
  aFeedbackFloorMinResponses: 5,
  minFactorsWithData: 3,
  ...overrides,
});

const row = (
  id: string,
  score: number,
  {
    status = "Verified",
    factors = {},
    withData = ["SyllabusAvailable", "ProviderReliability", "ListingIntegrity"],
    feedbackResponses = 0,
  }: {
    status?: CourseQualityStatus;
    factors?: Partial<Record<CourseGradeFactor, number>>;
    withData?: CourseGradeFactor[];
    feedbackResponses?: number;
  } = {},
): CourseGradeFactorRow => ({
  opportunityId: id,
  opportunityTitle: `Course ${id}`,
  organizationName: "Org",
  status,
  grade: "Ungraded",
  score: 0,
  factors: {
    ...(Object.fromEntries(GRADE_FACTORS.map((f) => [f, score])) as Record<
      CourseGradeFactor,
      number
    >),
    ...factors,
  },
  factorsWithData: withData,
  feedbackResponses,
});

/** 20 verified courses scoring 100, 95, ... 5 (ids c0..c19). */
const ladder = () =>
  Array.from({ length: 20 }, (_, i) => row(`c${i}`, 100 - i * 5));

describe("weights and score", () => {
  test("weights normalise to shares of 100", () => {
    const weights = normaliseWeights(rubric());
    const total = Object.values(weights).reduce((a, b) => a + b, 0);
    assert.ok(Math.abs(total - 100) < 1e-9);
    assert.equal(weights.LearnerFeedback, 20);
  });

  test("all-zero weights share equally", () => {
    const zero = rubric();
    for (const f of GRADE_FACTORS) zero.factors[f].weight = 0;
    assert.equal(normaliseWeights(zero).Accessibility, 12.5);
  });

  test("weighted score is the weighted mean, rounded", () => {
    const r = row("x", 0, { factors: { LearnerFeedback: 100 } });
    assert.equal(weightedScore(r, rubric()), 20);
    const doubled = rubric();
    doubled.factors.LearnerFeedback.weight = 120; // 120 / 200
    assert.equal(weightedScore(r, doubled), 60);
  });
});

describe("ungraded rules", () => {
  test("NotVerified and thin data are ungraded and left out of the percentiles", () => {
    const rows = [
      ...ladder(),
      row("nv", 100, { status: "NotVerified" }),
      row("thin", 100, {
        withData: ["Completion", "Completion", "CourseLength"],
      }),
    ];
    const results = regradeCatalogue(rows, rubric());
    assert.equal(results.get("nv")!.grade, "Ungraded");
    assert.equal(results.get("nv")!.percentile, null);
    assert.equal(results.get("thin")!.grade, "Ungraded");
    assert.match(results.get("thin")!.caps[0]!, /2 of the 3/);
    // ladder alone decides the bands: 15% of 20 = 3 A
    assert.equal(gradeDistribution(results.values()).A, 3);
  });
});

describe("bands", () => {
  test("percentile bands with cumulative shares", () => {
    const results = regradeCatalogue(ladder(), rubric());
    const dist = gradeDistribution(results.values());
    assert.deepEqual(dist, { A: 3, B: 5, C: 7, D: 5, Ungraded: 0 });
    assert.equal(results.get("c0")!.percentile, 100);
    assert.equal(results.get("c0")!.gradeLabel, "Top pick");
    assert.equal(results.get("c19")!.percentile, 5);
  });

  test("a tied group on a cut-off goes to the lower band", () => {
    const rows = ladder();
    // c2 and c3 tie on everything, across the A cut-off at rank 3
    rows[3] = row("c3", 90);
    rows[2] = row("c2", 90);
    const results = regradeCatalogue(rows, rubric());
    assert.equal(results.get("c2")!.grade, "B");
    assert.equal(results.get("c3")!.grade, "B");
    assert.equal(gradeDistribution(results.values()).A, 2);
  });

  test("ties break on feedback before falling to the lower band", () => {
    const rows = ladder();
    rows[2] = row("c2", 90, {
      factors: { LearnerFeedback: 91, SyllabusAvailable: 89 },
    });
    rows[3] = row("c3", 90);
    const results = regradeCatalogue(rows, rubric());
    assert.equal(results.get("c2")!.grade, "A");
    assert.equal(results.get("c3")!.grade, "B");
  });

  test("a band's minimum score pushes a course down", () => {
    const rows = Array.from({ length: 20 }, (_, i) => row(`c${i}`, 60 - i));
    const results = regradeCatalogue(rows, rubric());
    assert.equal(gradeDistribution(results.values()).A, 0);
    assert.equal(results.get("c0")!.grade, "B");
    assert.match(results.get("c0")!.caps[0]!, /below the A minimum of 65/);
  });
});

describe("caps", () => {
  test("NeedsReview reaches B at most", () => {
    const rows = ladder();
    rows[0] = row("c0", 100, { status: "NeedsReview" });
    const results = regradeCatalogue(rows, rubric());
    assert.equal(results.get("c0")!.grade, "B");
    assert.match(results.get("c0")!.caps.join(), /waiting for review/);
  });

  test("NeedsReview cap follows the rubric", () => {
    const rows = ladder();
    rows[0] = row("c0", 100, { status: "NeedsReview" });
    const results = regradeCatalogue(
      rows,
      rubric({ needsReviewMaxGrade: "C" }),
    );
    assert.equal(results.get("c0")!.grade, "C");
  });

  test("A floors: syllabus drops to B", () => {
    const rows = ladder();
    rows[0] = row("c0", 100, { factors: { SyllabusAvailable: 40 } });
    const results = regradeCatalogue(rows, rubric());
    assert.equal(results.get("c0")!.grade, "B");
    assert.match(results.get("c0")!.caps[0]!, /Syllabus 40/);
  });

  test("A floors: feedback applies only with enough answers", () => {
    const answered = (feedbackResponses: number) => {
      const rows = ladder();
      rows[0] = row("c0", 100, {
        factors: { LearnerFeedback: 50 },
        withData: ["LearnerFeedback", "SyllabusAvailable", "Completion"],
        feedbackResponses,
      });
      return regradeCatalogue(rows, rubric()).get("c0")!.grade;
    };
    assert.equal(answered(4), "A");
    assert.equal(answered(5), "B");
  });

  test("no feedback data: the feedback floor does not apply", () => {
    const rows = ladder();
    rows[0] = row("c0", 100, { factors: { LearnerFeedback: 50 } });
    assert.equal(regradeCatalogue(rows, rubric()).get("c0")!.grade, "A");
  });
});

describe("band shares", () => {
  test("own shares from cumulative top percents, and back", () => {
    const bands = rubric().bands;
    assert.deepEqual(bandShares(bands), [15, 25, 35, 25]);
    const next = withBandShare(bands, 0, 20);
    assert.deepEqual(
      next.map((b) => b.topPercent),
      [20, 45, 80, 100],
    );
    assert.deepEqual(bandShares(next), [20, 25, 35, 20]);
  });

  test("validation accepts the default rubric", () => {
    assert.deepEqual(validateRubric(rubric()), []);
  });

  test("validation: band shares as the API checks them", () => {
    const over = rubric();
    over.bands = withBandShare(over.bands, 2, 80);
    assert.ok(validateRubric(over).some((p) => /more than 100/.test(p)));

    const empty = rubric();
    empty.bands = withBandShare(empty.bands, 1, 0);
    assert.ok(
      validateRubric(empty).some((p) => /Band B needs a share/.test(p)),
    );

    const short = rubric();
    short.bands[3]!.topPercent = 99;
    assert.ok(
      validateRubric(short).some((p) => /last band must reach 100/.test(p)),
    );

    const missing = rubric();
    missing.bands = missing.bands.filter((b) => b.grade !== "C");
    missing.bands[2]!.topPercent = 100;
    assert.ok(validateRubric(missing).some((p) => /A, B, C and D/.test(p)));

    const unlabelled = rubric();
    unlabelled.bands[0]!.label = "  ";
    assert.ok(
      validateRubric(unlabelled).some((p) => /Band A needs a label/.test(p)),
    );
  });

  test("validation: weights, parameters and limits", () => {
    const zero = rubric();
    for (const f of GRADE_FACTORS) zero.factors[f].weight = 0;
    assert.ok(validateRubric(zero).some((p) => /more than zero/.test(p)));

    const negative = rubric();
    negative.factors.Completion.weight = -1;
    assert.ok(validateRubric(negative).some((p) => /negative weight/.test(p)));

    const parameter = rubric();
    parameter.factors.CourseLength.parameters = { shortHours: -2 };
    assert.ok(
      validateRubric(parameter).some((p) => /negative parameter/.test(p)),
    );

    const floors = rubric({ aMinSyllabusScore: 101, minFactorsWithData: 9 });
    const problems = validateRubric(floors);
    assert.ok(problems.some((p) => /syllabus floor/.test(p)));
    assert.ok(problems.some((p) => /factors needed/.test(p)));
  });
});

describe("courses waiting for review", () => {
  test("Ungraded: left out of the grades and the percentiles", () => {
    const rows = [...ladder(), row("nr", 100, { status: "NeedsReview" })];
    const results = regradeCatalogue(
      rows,
      rubric({ needsReviewMaxGrade: "Ungraded" }),
    );
    const waiting = results.get("nr")!;
    assert.equal(waiting.grade, "Ungraded");
    assert.equal(waiting.gradeLabel, "Not graded");
    assert.equal(waiting.percentile, null);
    assert.deepEqual(waiting.caps, [NEEDS_REVIEW_UNGRADED_REASON]);
    assert.ok(isWaitingForReviewCap(waiting.caps));
    // the ladder alone decides the bands, as if the waiting course were not there
    assert.equal(results.get("c0")!.percentile, 100);
    assert.deepEqual(gradeDistribution(results.values()), {
      A: 3,
      B: 5,
      C: 7,
      D: 5,
      Ungraded: 1,
    });
  });

  test("Ungraded does not touch verified courses", () => {
    const results = regradeCatalogue(
      ladder(),
      rubric({ needsReviewMaxGrade: "Ungraded" }),
    );
    assert.equal(results.get("c0")!.grade, "A");
  });

  test("not verified still reads as not verified", () => {
    const results = regradeCatalogue(
      [row("nv", 100, { status: "NotVerified" })],
      rubric({ needsReviewMaxGrade: "Ungraded" }),
    );
    assert.equal(
      results.get("nv")!.caps[0],
      "Not verified courses are not graded",
    );
  });

  test("validation accepts Ungraded and the A to D caps", () => {
    for (const grade of ["Ungraded", "A", "B", "C", "D"] as const)
      assert.deepEqual(
        validateRubric(rubric({ needsReviewMaxGrade: grade })),
        [],
      );
  });
});

describe("rounding", () => {
  test("half rounds away from zero, despite float noise", () => {
    assert.equal(roundHalfAway(54.5), 55);
    assert.equal(roundHalfAway(21.499999999999996), 22);
    assert.equal(roundHalfAway(21.4999), 21);
    assert.equal(roundHalfAway(-2.5), -3);
  });

  test("an exact .5 weighted score rounds up like the API", () => {
    // (0 × 0.1 + 43 × 0.1) / 0.2 is exactly 21.5, but floats give 21.499999999999996
    const tenths = rubric();
    for (const f of GRADE_FACTORS) tenths.factors[f].weight = 0;
    tenths.factors.LearnerFeedback.weight = 0.1;
    tenths.factors.SyllabusAvailable.weight = 0.1;
    const r = row("x", 0, { factors: { SyllabusAvailable: 43 } });
    assert.equal(weightedScore(r, tenths), 22);
  });
});
