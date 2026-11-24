import Link from "next/link";
import type { MyYoma } from "~/api/models/pathway";
import { BTN_SECONDARY } from "~/components/Common/buttonStyles";
import { goalMeta, nextStageLabel, pathProgress } from "~/lib/pathway";
import LearnerBadges from "./LearnerBadges";
import PathwayCourseRow from "./PathwayCourseRow";
import YoIdSummaryCard from "./YoIdSummaryCard";

// ─────────────────────────────────────────────────────────────────────────────
// "Your world, your Yoma": the YoID summary, the goal, the path as numbered stages of courses,
// open jobs at the end and the learner badges. Layout and copy follow the approved prototype.
// ─────────────────────────────────────────────────────────────────────────────

/** The number in the dot on the left of a stage, with the line down to the next stage. */
const StageDot: React.FC<{ children: React.ReactNode; tone?: "job" }> = ({
  children,
  tone,
}) => (
  <span
    aria-hidden="true"
    className={`absolute top-0.5 left-0 flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold ${
      tone === "job" ? "bg-yellow text-black" : "bg-purple text-white"
    }`}
  >
    {children}
  </span>
);

const MyYomaView: React.FC<{
  myYoma: MyYoma;
  onChangeAnswers: () => void;
}> = ({ myYoma, onChangeAnswers }) => {
  const goal = myYoma.goalLabel || myYoma.profile.goalLabel;
  const progress = pathProgress(myYoma);
  const country = myYoma.profile.countryName;
  let courseNumber = 0;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      {/* HELLO */}
      <div className="flex flex-wrap items-end justify-between gap-3">
        <p className="text-gray-dark max-w-xl text-sm md:text-base">
          A path from where you are now to {goal.toLowerCase()}, built from
          courses on Yoma.
        </p>
        <button
          type="button"
          className={BTN_SECONDARY}
          onClick={onChangeAnswers}
        >
          Change my answers
        </button>
      </div>

      <YoIdSummaryCard yoId={myYoma.yoId} profile={myYoma.profile} />

      {/* GOAL */}
      <section
        aria-label="Your goal"
        className="bg-yellow-tint flex flex-col gap-2 rounded-xl px-4 py-3.5"
      >
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
          <div>
            <p className="text-gray-dark text-xs">Your goal</p>
            <p className="font-nunito text-lg font-black text-black">{goal}</p>
          </div>
          <p className="text-gray-dark text-sm">{goalMeta(myYoma)}</p>
        </div>
        <div className="flex items-center gap-3">
          <progress
            className="progress progress-primary h-1.5 w-full bg-white"
            value={progress}
            max={100}
            aria-label={`Completed ${myYoma.completedCourses} of ${myYoma.totalCourses} courses`}
          />
          <p className="shrink-0 text-xs font-semibold text-black">
            Completed {myYoma.completedCourses} of {myYoma.totalCourses}
          </p>
        </div>
      </section>

      {myYoma.throughLine && (
        <p className="text-gray-dark max-w-prose text-sm">
          {myYoma.throughLine}
        </p>
      )}

      {/* THE PATH */}
      <ol aria-label="Your path" className="flex flex-col">
        {myYoma.stages.map((stage) => {
          const next = nextStageLabel(stage.leadsTo);
          return (
            <li
              key={stage.order}
              className="before:bg-gray relative pb-6 pl-10 before:absolute before:top-9 before:bottom-1 before:left-[13px] before:w-0.5"
            >
              <StageDot>{stage.order}</StageDot>
              <h3 className="font-nunito text-lg font-black text-black">
                <span className="sr-only">Stage {stage.order}: </span>
                {stage.name}
              </h3>
              <p className="text-gray-dark mt-0.5 mb-3 max-w-prose text-sm">
                {stage.why}
                {stage.shortened && (
                  <>
                    {" "}
                    <span className="bg-green-light text-green inline-block rounded-md px-2 py-0.5 text-xs font-semibold">
                      Shortened: you said you already have this
                    </span>
                  </>
                )}
                {next && <> {next}.</>}
              </p>

              {stage.courses.length > 0 ? (
                <ol className="flex flex-col gap-2.5">
                  {stage.courses.map((course) => {
                    courseNumber += 1;
                    return (
                      <li key={course.opportunityId}>
                        <PathwayCourseRow
                          course={course}
                          number={courseNumber}
                        />
                      </li>
                    );
                  })}
                </ol>
              ) : (
                <p className="text-gray-dark text-sm">
                  No matching course on Yoma yet.
                </p>
              )}
            </li>
          );
        })}

        {/* JOBS */}
        <li className="relative pl-10">
          <StageDot tone="job">★</StageDot>
          <h3 className="font-nunito text-lg font-black text-black">
            Apply for jobs
          </h3>
          <p className="text-gray-dark mt-0.5 mb-3 text-sm">
            Open roles on Yoma that this path prepares you for
            {country ? ` in ${country}` : ""}.
          </p>
          {myYoma.jobs.length > 0 ? (
            <ul className="grid grid-cols-1 gap-2.5 sm:grid-cols-2 lg:grid-cols-3">
              {myYoma.jobs.map((job) => (
                <li key={job.opportunityId}>
                  <Link
                    href={`/opportunities/${encodeURIComponent(job.opportunityId)}`}
                    className="border-gray hover:border-purple-light focus-visible:outline-purple flex h-full flex-col rounded-xl border bg-white px-3.5 py-3 focus-visible:outline-2 focus-visible:outline-offset-2"
                  >
                    <span className="text-sm font-semibold text-black">
                      {job.title}
                    </span>
                    <span className="text-gray-dark text-xs">
                      {[job.organizationName, job.countries.join(", ")]
                        .filter(Boolean)
                        .join(" · ")}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-gray-dark text-sm">
              No matching jobs listed right now.
            </p>
          )}
        </li>
      </ol>

      <LearnerBadges badges={myYoma.badges} />
    </div>
  );
};

export default MyYomaView;
