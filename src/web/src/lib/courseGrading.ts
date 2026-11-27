import type {
  CourseGrade,
  CourseGradeBand,
  CourseGradeFactor,
  JobGradeFactor,
  CourseGradeFactorRow,
  CourseGradeRubric,
} from "~/api/models/courseQuality";

// ─────────────────────────────────────────────────────────────────────────────
// Regrades the catalogue in the browser from stored factor scores, so the rubric page previews
// a change instantly. Mirrors the API's rules (docs/work/active/course-quality-judger/
// iteration-2.md): factor scores are absolute, only the bands are percentiles.
//
// - NotVerified courses, and courses with data on fewer than `minFactorsWithData` factors, are
//   Ungraded and left out of the percentiles.
// - The weighted score is the weight-normalised mean of the factor scores, rounded to a whole
//   number as the API stores it.
// - Ranking: score, then feedback, syllabus, completion and length. A group tied on all of them
//   that straddles a cut-off goes to the lower band, so A stays at or under its share.
// - A band also needs its `minScore`; a course below it falls to the next band.
// - NeedsReview reaches `needsReviewMaxGrade` at most. "Ungraded" (the default) leaves those
//   courses ungraded and out of the percentiles until a reviewer confirms them.
// - A also needs Verified, syllabus ≥ `aMinSyllabusScore`, and feedback ≥ `aMinFeedbackScore`
//   once the course has `aFeedbackFloorMinResponses` answers. Otherwise B.
// ─────────────────────────────────────────────────────────────────────────────

export const GRADE_FACTORS: CourseGradeFactor[] = [
  "LearnerFeedback",
  "SyllabusAvailable",
  "ProviderReliability",
  "Comprehensiveness",
  "CourseLength",
  "Completion",
  "ListingIntegrity",
  "Accessibility",
];

export const GRADE_FACTOR_LABELS: Record<CourseGradeFactor, string> = {
  LearnerFeedback: "Learner feedback",
  SyllabusAvailable: "Syllabus available",
  ProviderReliability: "Provider reliability",
  Comprehensiveness: "Comprehensiveness",
  CourseLength: "Course length",
  Completion: "Completion",
  ListingIntegrity: "Listing integrity",
  Accessibility: "Accessibility",
  Outcomes: "Outcomes (what completers did next)",
  DropOff: "Drop-off (collecting from the pilot)",
};

export const JOB_GRADE_FACTOR_LABELS: Record<JobGradeFactor, string> = {
  PayTransparency: "Pay stated",
  JobClarity: "Job details",
  YouthFit: "Youth fit",
  ListingSafety: "Listing safety",
  PartnerReliability: "Partner reliability",
  ApplicantFeedback: "Applicant feedback",
};

/** The label of a course or job grade factor, as a grade breakdown shows it. */
export const gradeFactorLabel = (
  factor: CourseGradeFactor | JobGradeFactor,
): string =>
  (GRADE_FACTOR_LABELS as Record<string, string>)[factor] ??
  (JOB_GRADE_FACTOR_LABELS as Record<string, string>)[factor] ??
  factor;

export const GRADES: CourseGrade[] = ["A", "B", "C", "D", "Ungraded"];

export const UNGRADED_LABEL = "Not graded";

/** The cap reason for a course waiting for review when the rubric does not grade those. */
export const NEEDS_REVIEW_UNGRADED_REASON =
  "Not graded until a reviewer confirms it";

/** True when a cap says the course is ungraded only because it waits for review. */
export const isWaitingForReviewCap = (caps: string[] | null | undefined) =>
  !!caps?.includes(NEEDS_REVIEW_UNGRADED_REASON);

/** Tie-breakers after the score, in order. */
const TIE_BREAKERS: CourseGradeFactor[] = [
  "LearnerFeedback",
  "SyllabusAvailable",
  "Completion",
  "CourseLength",
];

const GRADE_RANK: Record<CourseGrade, number> = {
  A: 0,
  B: 1,
  C: 2,
  D: 3,
  Ungraded: 4,
};

/** True when `a` is a better grade than `b`. Ungraded is worse than any grade. */
export const isBetterGrade = (a: CourseGrade, b: CourseGrade): boolean =>
  GRADE_RANK[a] < GRADE_RANK[b];

export interface RegradedCourse {
  opportunityId: string;
  grade: CourseGrade;
  gradeLabel: string;
  /** Weighted score, 0 to 100, rounded. */
  score: number;
  /** Share of graded courses this one scores at or above. Null when ungraded. */
  percentile: number | null;
  /** Rules that held the grade below what its rank alone would give, or why it is ungraded. */
  caps: string[];
}

/** Weights as shares of 100. All-zero weights share equally. */
export const normaliseWeights = (
  rubric: Pick<CourseGradeRubric, "factors">,
): Record<CourseGradeFactor, number> => {
  const raw = GRADE_FACTORS.map((factor) =>
    Math.max(0, rubric.factors[factor]?.weight ?? 0),
  );
  const total = raw.reduce((sum, weight) => sum + weight, 0);
  return Object.fromEntries(
    GRADE_FACTORS.map((factor, index) => [
      factor,
      total > 0 ? (raw[index]! / total) * 100 : 100 / GRADE_FACTORS.length,
    ]),
  ) as Record<CourseGradeFactor, number>;
};

const factorScore = (
  row: CourseGradeFactorRow,
  factor: CourseGradeFactor,
  rubric: CourseGradeRubric,
): number => {
  const value = row.factors[factor];
  return typeof value === "number"
    ? value
    : (rubric.factors[factor]?.neutralScore ?? 50);
};

/**
 * Rounds half away from zero like the API's decimal `Math.Round(..., AwayFromZero)`. The value is
 * first cut to 9 decimals, so float noise (54.49999999999999 for an exact 54.5) cannot tip it.
 */
export const roundHalfAway = (value: number): number => {
  const exact = Number(Math.abs(value).toFixed(9));
  return Math.sign(value) * Math.round(exact);
};

/** sum(score × weight) / sum(weights), with the raw weights as the API does, rounded and clamped 0–100. */
export const weightedScore = (
  row: CourseGradeFactorRow,
  rubric: CourseGradeRubric,
): number => {
  let total = 0;
  let sum = 0;
  for (const factor of GRADE_FACTORS) {
    const weight = Math.max(0, rubric.factors[factor]?.weight ?? 0);
    total += weight;
    sum += factorScore(row, factor, rubric) * weight;
  }
  if (total <= 0) return 0;
  return Math.max(0, Math.min(100, roundHalfAway(sum / total)));
};

export const gradeLabelOf = (
  grade: CourseGrade,
  bands: CourseGradeBand[],
): string =>
  grade === "Ungraded"
    ? UNGRADED_LABEL
    : (bands.find((band) => band.grade === grade)?.label ?? grade);

/** Why a course is not graded at all, or null when it can be. */
export const ungradedReason = (
  row: CourseGradeFactorRow,
  rubric: CourseGradeRubric,
): string | null => {
  if (row.status === "NotVerified")
    return "Not verified courses are not graded";
  if (row.status === "NeedsReview" && rubric.needsReviewMaxGrade === "Ungraded")
    return NEEDS_REVIEW_UNGRADED_REASON;
  const withData = new Set(row.factorsWithData).size;
  if (withData < rubric.minFactorsWithData)
    return `Data on ${withData} of the ${rubric.minFactorsWithData} factors needed to grade`;
  return null;
};

interface Ranked {
  row: CourseGradeFactorRow;
  score: number;
  keys: number[];
}

const compareRanked = (a: Ranked, b: Ranked): number => {
  for (let i = 0; i < a.keys.length; i++) {
    const diff = b.keys[i]! - a.keys[i]!;
    if (diff !== 0) return diff;
  }
  return 0;
};

/** Cumulative cut-offs as course counts: band i holds ranks 1..cutoffs[i]. The last holds all. */
const bandCutoffs = (bands: CourseGradeBand[], total: number): number[] =>
  bands.map((band, index) =>
    index === bands.length - 1
      ? total
      : Math.floor((total * band.topPercent) / 100 + 1e-9),
  );

export const regradeCatalogue = (
  rows: CourseGradeFactorRow[],
  rubric: CourseGradeRubric,
): Map<string, RegradedCourse> => {
  const results = new Map<string, RegradedCourse>();
  const bands = rubric.bands.filter((band) => band.grade !== "Ungraded");

  const ranked: Ranked[] = [];
  for (const row of rows) {
    const score = weightedScore(row, rubric);
    const reason = ungradedReason(row, rubric);
    if (reason || bands.length === 0) {
      results.set(row.opportunityId, {
        opportunityId: row.opportunityId,
        grade: "Ungraded",
        gradeLabel: UNGRADED_LABEL,
        score,
        percentile: null,
        caps: reason ? [reason] : ["The rubric has no bands"],
      });
      continue;
    }
    ranked.push({
      row,
      score,
      keys: [
        score,
        ...TIE_BREAKERS.map((factor) => factorScore(row, factor, rubric)),
      ],
    });
  }

  ranked.sort(compareRanked);
  const total = ranked.length;
  const cutoffs = bandCutoffs(bands, total);
  const last = bands.length - 1;

  let start = 0;
  while (start < total) {
    // the whole tied group shares one band: the best one it fits inside entirely
    let end = start;
    while (
      end + 1 < total &&
      compareRanked(ranked[start]!, ranked[end + 1]!) === 0
    )
      end++;
    const rankBand = cutoffs.findIndex((cutoff) => end + 1 <= cutoff);
    const percentile = roundHalfAway(((total - start) * 100) / total);

    for (let i = start; i <= end; i++) {
      const { row, score } = ranked[i]!;
      const caps: string[] = [];
      let band = rankBand < 0 ? last : rankBand;

      const applyMinScore = () => {
        while (band < last && score < bands[band]!.minScore) {
          caps.push(
            `Score ${score} is below the ${bands[band]!.grade} minimum of ${bands[band]!.minScore}`,
          );
          band++;
        }
      };
      applyMinScore();

      if (row.status === "NeedsReview") {
        const maxIndex = bands.findIndex(
          (b) => b.grade === rubric.needsReviewMaxGrade,
        );
        if (maxIndex > band) {
          caps.push(
            `Courses waiting for review reach ${rubric.needsReviewMaxGrade} at most`,
          );
          band = maxIndex;
          applyMinScore();
        }
      }

      if (bands[band]!.grade === "A") {
        const floors: string[] = [];
        if (row.status !== "Verified") floors.push("A needs a verified course");
        const syllabus = factorScore(row, "SyllabusAvailable", rubric);
        if (syllabus < rubric.aMinSyllabusScore)
          floors.push(
            `Syllabus ${syllabus} is below the A floor of ${rubric.aMinSyllabusScore}`,
          );
        const feedbackCounts =
          row.feedbackResponses >= rubric.aFeedbackFloorMinResponses;
        const feedback = factorScore(row, "LearnerFeedback", rubric);
        if (feedbackCounts && feedback < rubric.aMinFeedbackScore)
          floors.push(
            `Feedback ${feedback} is below the A floor of ${rubric.aMinFeedbackScore}`,
          );
        if (floors.length > 0 && band < last) {
          caps.push(...floors);
          band++;
          applyMinScore();
        }
      }

      const grade = bands[band]!.grade;
      results.set(row.opportunityId, {
        opportunityId: row.opportunityId,
        grade,
        gradeLabel: bands[band]!.label,
        score,
        percentile,
        caps,
      });
    }
    start = end + 1;
  }

  return results;
};

export const gradeDistribution = (
  results: Iterable<{ grade: CourseGrade }>,
): Record<CourseGrade, number> => {
  const counts: Record<CourseGrade, number> = {
    A: 0,
    B: 0,
    C: 0,
    D: 0,
    Ungraded: 0,
  };
  for (const { grade } of results) counts[grade]++;
  return counts;
};

/** Each band's own share (not cumulative), from its cumulative `topPercent`. */
export const bandShares = (bands: CourseGradeBand[]): number[] =>
  bands.map((band, index) =>
    index === bands.length - 1
      ? Math.max(0, 100 - (bands[index - 1]?.topPercent ?? 0))
      : band.topPercent - (bands[index - 1]?.topPercent ?? 0),
  );

/** Sets band `index`'s own share, keeping the others' shares. The last band takes the rest. */
export const withBandShare = (
  bands: CourseGradeBand[],
  index: number,
  share: number,
): CourseGradeBand[] => {
  const shares = bandShares(bands);
  shares[index] = Math.max(0, share);
  let cumulative = 0;
  return bands.map((band, i) => {
    cumulative += shares[i]!;
    return {
      ...band,
      topPercent: i === bands.length - 1 ? 100 : cumulative,
    };
  });
};

export const MAX_LABEL_LENGTH = 50;
export const MAX_VERSION_LENGTH = 50;
const REQUIRED_BANDS: CourseGrade[] = ["A", "B", "C", "D"];

/**
 * Problems that would make the API reject the rubric, in plain words. Empty when it can be saved.
 * Mirrors CourseQualityGrading.Validate in the API, and also requires all four bands, A to D.
 */
export const validateRubric = (rubric: CourseGradeRubric): string[] => {
  const problems: string[] = [];

  const version = rubric.version?.trim() ?? "";
  if (!version || version.length > MAX_VERSION_LENGTH)
    problems.push(`The version must be 1 to ${MAX_VERSION_LENGTH} characters.`);

  let total = 0;
  for (const factor of GRADE_FACTORS) {
    const options = rubric.factors?.[factor];
    const label = GRADE_FACTOR_LABELS[factor];
    if (!options) {
      problems.push(`${label} needs a weight.`);
      continue;
    }
    if (!Number.isFinite(options.weight) || options.weight < 0)
      problems.push(`${label} has a negative weight.`);
    else total += options.weight;
    if (options.neutralScore < 0 || options.neutralScore > 100)
      problems.push(`${label} needs a neutral score from 0 to 100.`);
    if (Object.values(options.parameters ?? {}).some((value) => value < 0))
      problems.push(`${label} has a negative parameter.`);
  }
  if (total <= 0) problems.push("The weights must add up to more than zero.");

  const bands = rubric.bands ?? [];
  const grades = bands.map((band) => band.grade);
  if (
    grades.length !== REQUIRED_BANDS.length ||
    REQUIRED_BANDS.some((grade, i) => grades[i] !== grade)
  )
    problems.push("The rubric needs the bands A, B, C and D, in that order.");

  bands.forEach((band, i) => {
    const label = band.label?.trim() ?? "";
    if (!label || label.length > MAX_LABEL_LENGTH)
      problems.push(
        `Band ${band.grade} needs a label of 1 to ${MAX_LABEL_LENGTH} characters.`,
      );
    const previous = i > 0 ? bands[i - 1]!.topPercent : 0;
    if (band.topPercent <= previous)
      problems.push(`Band ${band.grade} needs a share above 0%.`);
    if (band.topPercent > 100)
      problems.push("The band shares add up to more than 100%.");
    if (band.minScore < 0 || band.minScore > 100)
      problems.push(`Band ${band.grade} needs a minimum score from 0 to 100.`);
  });
  if (bands.length > 0 && bands[bands.length - 1]!.topPercent !== 100)
    problems.push("The last band must reach 100%.");

  if (
    rubric.needsReviewMaxGrade !== "Ungraded" &&
    !REQUIRED_BANDS.includes(rubric.needsReviewMaxGrade)
  )
    problems.push(
      "Courses waiting for review must be not graded, or reach A, B, C or D at most.",
    );
  if (rubric.aMinSyllabusScore < 0 || rubric.aMinSyllabusScore > 100)
    problems.push("The syllabus floor for A must be from 0 to 100.");
  if (rubric.aMinFeedbackScore < 0 || rubric.aMinFeedbackScore > 100)
    problems.push("The feedback floor for A must be from 0 to 100.");
  if (rubric.aFeedbackFloorMinResponses < 0)
    problems.push(
      "The answers needed for the feedback floor cannot be negative.",
    );
  if (
    rubric.minFactorsWithData < 0 ||
    rubric.minFactorsWithData > GRADE_FACTORS.length
  )
    problems.push(
      `The factors needed to grade must be from 0 to ${GRADE_FACTORS.length}.`,
    );

  return [...new Set(problems)];
};
