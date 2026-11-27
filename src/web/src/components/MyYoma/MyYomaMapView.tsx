import type { JourneyMapProps } from "~/components/MyYoma/JourneyMap/types";
import JourneyMap from "~/components/MyYoma/JourneyMap/JourneyMap";
import LearnerBadges from "./LearnerBadges";
import YoIdSummaryCard from "./YoIdSummaryCard";

// ─────────────────────────────────────────────────────────────────────────────
// My Yoma with a path (iteration 4): the YoID card above the journey map, the learner badges
// below it. The map is W1's `JourneyMap`; this view only frames it and passes the page's wiring.
// ─────────────────────────────────────────────────────────────────────────────

const MyYomaMapView: React.FC<JourneyMapProps> = (props) => {
  const { myYoma } = props;
  const goal = myYoma.goalLabel || myYoma.profile.goalLabel;

  return (
    <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <p className="text-gray-dark max-w-xl text-sm md:text-base">
          A path from where you are now to {goal.toLowerCase()}, built from
          courses, practice and jobs on Yoma.
        </p>
        {/* "Change my answers" lives in the journey map's own header */}
      </div>

      <YoIdSummaryCard yoId={myYoma.yoId} profile={myYoma.profile} />

      <JourneyMap {...props} />

      <LearnerBadges badges={myYoma.badges} />
    </div>
  );
};

export default MyYomaMapView;
