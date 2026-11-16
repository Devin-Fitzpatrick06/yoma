import type {
  CourseFeedbackSummary,
  CourseFeedbackTagCount,
} from "~/api/models/courseQuality";

/** How many good and bad tags the panel lists. */
export const SUMMARY_TOP_TAGS = 5;

export interface SummarySplit {
  yes: number;
  no: number;
  /** Whole-number share of yes among those who answered; `null` when nobody answered. */
  yesPercent: number | null;
}

export interface SummaryTagBar {
  key: string;
  label: string;
  count: number;
  /** Share of all answers that picked the tag, 0–100, for the bar width. */
  percent: number;
}

export interface FeedbackSummaryView {
  responses: number;
  completed: number;
  averageRating: string | null;
  adjustedRating: string | null;
  recommend: SummarySplit;
  matched: SummarySplit;
  good: SummaryTagBar[];
  bad: SummaryTagBar[];
  /** Always empty unless comments are to be shown. */
  comments: string[];
}

const formatRating = (value: number | null | undefined): string | null =>
  value == null || !Number.isFinite(value) ? null : value.toFixed(1);

export const toSplit = (yes: number, no: number): SummarySplit => {
  const total = Math.max(0, yes) + Math.max(0, no);
  return {
    yes: Math.max(0, yes),
    no: Math.max(0, no),
    yesPercent: total > 0 ? Math.round((Math.max(0, yes) / total) * 100) : null,
  };
};

export const toTagBars = (
  tags: CourseFeedbackTagCount[] | null | undefined,
  responses: number,
): SummaryTagBar[] =>
  [...(tags ?? [])]
    .filter((tag) => tag.count > 0)
    .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label))
    .slice(0, SUMMARY_TOP_TAGS)
    .map((tag) => ({
      key: tag.key,
      label: tag.label,
      count: tag.count,
      percent:
        responses > 0
          ? Math.min(100, Math.round((tag.count / responses) * 100))
          : 0,
    }));

/** `null` is the empty state: no summary, or no learner answers yet. */
export const toFeedbackSummaryView = (
  summary: CourseFeedbackSummary | null | undefined,
  showComments: boolean,
): FeedbackSummaryView | null => {
  if (!summary || !(summary.responses > 0)) return null;

  return {
    responses: summary.responses,
    completed: Math.max(0, summary.completed ?? 0),
    averageRating: formatRating(summary.averageRating),
    adjustedRating: formatRating(summary.adjustedRating),
    recommend: toSplit(summary.wouldRecommendYes, summary.wouldRecommendNo),
    matched: toSplit(
      summary.matchedDescriptionYes,
      summary.matchedDescriptionNo,
    ),
    good: toTagBars(summary.good, summary.responses),
    bad: toTagBars(summary.bad, summary.responses),
    comments: showComments
      ? (summary.recentComments ?? []).filter((c) => c?.trim())
      : [],
  };
};
