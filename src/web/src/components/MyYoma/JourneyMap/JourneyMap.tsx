import type { JourneyMapProps } from "./types";

// ─────────────────────────────────────────────────────────────────────────────
// PLACEHOLDER (W2, iteration 4). Engineer W1 builds the real journey map in parallel; at merge the
// lead replaces this file with W1's `JourneyMap.tsx`. It only exists so `pages/my-yoma` compiles
// and its wiring (lock, swap, step events, change answers) can be exercised. No design intent.
// ─────────────────────────────────────────────────────────────────────────────

const JourneyMap: React.FC<JourneyMapProps> = ({
  myYoma,
  onLock,
  loadAlternatives,
  onSwap,
  onChangeAnswers,
}) => (
  <section
    aria-label="Your journey (placeholder)"
    data-placeholder="journey-map"
    className="border-gray flex flex-col gap-3 rounded-xl border border-dashed bg-white p-4"
  >
    <p className="text-gray-dark text-xs">
      Placeholder journey map: {myYoma.startLabel} → {myYoma.destinationLabel}
    </p>
    <ol className="flex flex-col gap-3">
      {myYoma.stages.map((stage) => (
        <li key={stage.order}>
          <h3 className="font-semibold text-black">{stage.name}</h3>
          <ul className="flex flex-col gap-1">
            {stage.courses.map((course) => (
              <li
                key={course.opportunityId}
                className="flex flex-wrap items-center gap-2 text-sm"
              >
                <span>{course.title}</span>
                <button
                  type="button"
                  className="underline"
                  onClick={() =>
                    void onLock(course.opportunityId, !course.locked)
                  }
                >
                  {course.locked ? "Unlock" : "Lock"}
                </button>
                <button
                  type="button"
                  className="underline"
                  onClick={async () => {
                    const [first] = await loadAlternatives(
                      course.opportunityId,
                    );
                    if (first)
                      await onSwap(course.opportunityId, first.opportunityId);
                  }}
                >
                  Swap
                </button>
              </li>
            ))}
          </ul>
        </li>
      ))}
    </ol>
    <button
      type="button"
      className="self-start underline"
      onClick={onChangeAnswers}
    >
      Change my answers
    </button>
  </section>
);

export default JourneyMap;
