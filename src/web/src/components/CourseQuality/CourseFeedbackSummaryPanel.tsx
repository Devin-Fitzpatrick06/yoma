import { useId } from "react";
import { IoChatbubbleOutline, IoStar } from "react-icons/io5";
import type { CourseFeedbackSummary } from "~/api/models/courseQuality";
import {
  toFeedbackSummaryView,
  type SummarySplit,
  type SummaryTagBar,
} from "./courseFeedbackSummaryModel";

// ─────────────────────────────────────────────────────────────────────────────
// What learners said about a course after they came back from it: used by Yoma reviewers (with
// comments) and by the course's partner (never with comments, as they may come from children).
// Text and plain bars only, so it costs nothing to render on a slow phone.
// ─────────────────────────────────────────────────────────────────────────────

const plural = (count: number, one: string, many: string) =>
  `${count} ${count === 1 ? one : many}`;

const Stat: React.FC<{
  label: string;
  value: React.ReactNode;
  detail?: string;
}> = ({ label, value, detail }) => (
  <div className="bg-gray-light flex min-w-0 flex-col rounded-lg px-3 py-2">
    <dt className="text-gray-dark text-xs font-semibold">{label}</dt>
    <dd className="font-nunito text-lg leading-snug font-black text-black">
      {value}
    </dd>
    {detail && (
      <dd className="text-gray-dark text-xs wrap-break-word">{detail}</dd>
    )}
  </div>
);

const Split: React.FC<{ label: string; split: SummarySplit }> = ({
  label,
  split,
}) => (
  <div className="flex flex-col gap-1">
    <div className="flex flex-wrap items-baseline justify-between gap-x-3 text-sm">
      <span className="font-semibold text-black">{label}</span>
      <span className="text-gray-dark text-xs">
        {split.yesPercent == null
          ? "No answers"
          : `${split.yes} yes · ${split.no} no`}
      </span>
    </div>
    {split.yesPercent != null && (
      <div
        className="bg-gray flex h-2 overflow-hidden rounded-full"
        role="img"
        aria-label={`${split.yesPercent}% said yes`}
      >
        <div
          className="bg-purple h-full"
          style={{ width: `${split.yesPercent}%` }}
        />
      </div>
    )}
  </div>
);

const TagBars: React.FC<{
  title: string;
  tags: SummaryTagBar[];
  barClassName: string;
  responses: number;
}> = ({ title, tags, barClassName, responses }) => (
  <div className="flex min-w-0 flex-col gap-2">
    <h5 className="text-sm font-semibold text-black">{title}</h5>
    {tags.length === 0 ? (
      <p className="text-gray-dark text-xs">Nothing picked yet</p>
    ) : (
      <ul className="flex flex-col gap-1.5">
        {tags.map((tag) => (
          <li key={tag.key} className="flex flex-col gap-0.5">
            <div className="flex items-baseline justify-between gap-2 text-xs">
              <span className="min-w-0 wrap-break-word text-black">
                {tag.label}
              </span>
              <span className="text-gray-dark shrink-0">
                {tag.count}
                <span className="sr-only"> of {responses} learners</span>
              </span>
            </div>
            <div className="bg-gray-light h-1.5 overflow-hidden rounded-full">
              <div
                className={`h-full rounded-full ${barClassName}`}
                style={{ width: `${Math.max(tag.percent, 4)}%` }}
              />
            </div>
          </li>
        ))}
      </ul>
    )}
  </div>
);

const CourseFeedbackSummaryPanel: React.FC<{
  summary: CourseFeedbackSummary | null;
  showComments: boolean;
}> = ({ summary, showComments }) => {
  const headingId = useId();
  const view = toFeedbackSummaryView(summary, showComments);

  return (
    <section
      aria-labelledby={headingId}
      className="border-gray flex flex-col gap-3 rounded-lg border bg-white p-3 md:p-4"
    >
      <h4 id={headingId} className="text-sm font-semibold text-black">
        What learners said
      </h4>

      {!view ? (
        <p className="text-gray-dark text-sm">No learner answers yet</p>
      ) : (
        <>
          <dl className="grid grid-cols-2 gap-2 md:grid-cols-4">
            <Stat label="Answers" value={view.responses} />
            <Stat
              label="Average rating"
              value={
                view.averageRating ? (
                  <span className="flex items-center gap-1">
                    <IoStar
                      aria-hidden="true"
                      className="text-yellow h-4 w-4"
                    />
                    {view.averageRating}
                    <span className="sr-only"> out of 5</span>
                  </span>
                ) : (
                  "–"
                )
              }
            />
            <Stat
              label="Adjusted rating"
              value={view.adjustedRating ?? "–"}
              detail="Leans towards this provider's usual rating until there are enough answers"
            />
            <Stat
              label="Finished it"
              value={view.completed}
              detail={`of ${plural(view.responses, "learner", "learners")}`}
            />
          </dl>

          <div className="grid gap-3 md:grid-cols-2">
            <Split label="Would recommend it" split={view.recommend} />
            <Split label="Matched what Yoma said" split={view.matched} />
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <TagBars
              title="What was good"
              tags={view.good}
              barClassName="bg-purple"
              responses={view.responses}
            />
            <TagBars
              title="What could be better"
              tags={view.bad}
              barClassName="bg-orange"
              responses={view.responses}
            />
          </div>

          {showComments && (
            <div className="flex flex-col gap-2">
              <h5 className="flex items-center gap-1.5 text-sm font-semibold text-black">
                <IoChatbubbleOutline aria-hidden="true" className="h-4 w-4" />
                Recent comments
                <span className="text-gray-dark text-xs font-normal">
                  (Yoma team only)
                </span>
              </h5>
              {view.comments.length === 0 ? (
                <p className="text-gray-dark text-xs">No comments yet</p>
              ) : (
                <ul className="flex flex-col gap-1.5">
                  {view.comments.map((comment, index) => (
                    <li
                      key={`${index}-${comment.slice(0, 16)}`}
                      className="bg-gray-light rounded-lg px-3 py-2 text-sm wrap-break-word whitespace-pre-line text-black"
                    >
                      {comment}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </>
      )}
    </section>
  );
};

export default CourseFeedbackSummaryPanel;
