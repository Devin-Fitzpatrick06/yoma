import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { getFeedbackSummary } from "~/api/services/courseQualityLearner";
import CourseFeedbackSummaryPanel from "./CourseFeedbackSummaryPanel";

/**
 * What learners said about one course, fetched only once it scrolls near the screen, so a long
 * list costs one small request per course actually looked at. For partners pass
 * `showComments={false}`: comments are for the Yoma team only (the API leaves them out too).
 */
const CourseFeedbackLazySummary: React.FC<{
  opportunityId: string;
  showComments: boolean;
}> = ({ opportunityId, showComments }) => {
  const ref = useRef<HTMLDivElement>(null);
  const [near, setNear] = useState(false);

  useEffect(() => {
    const node = ref.current;
    if (!node || near) return;
    if (typeof IntersectionObserver === "undefined") {
      setNear(true);
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) setNear(true);
      },
      { rootMargin: "200px" },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [near]);

  const { data, isLoading, isError } = useQuery({
    queryKey: ["courseQuality", "feedbackSummary", opportunityId],
    queryFn: () => getFeedbackSummary(opportunityId),
    enabled: near,
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  return (
    <div ref={ref}>
      {!near || isLoading ? (
        <div className="border-gray text-gray-dark rounded-lg border p-3 text-sm">
          <span className="loading loading-dots loading-xs" aria-hidden />
          <span className="sr-only">Loading what learners said</span>
        </div>
      ) : isError ? (
        <p className="text-gray-dark text-sm">
          Learner answers could not be loaded right now.
        </p>
      ) : (
        <CourseFeedbackSummaryPanel
          summary={data ?? null}
          showComments={showComments}
        />
      )}
    </div>
  );
};

export default CourseFeedbackLazySummary;
