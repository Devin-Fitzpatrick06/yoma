import { useId, type ReactNode } from "react";
import { IoCheckmark } from "react-icons/io5";
import { threadLabel, type ThreadStep } from "~/lib/journey";

// ─────────────────────────────────────────────────────────────────────────────
// The journey's YoID thread (contract v1, "The YoID thread"): the label
// `YoID · {yoid} · mock-up` and the four steps, each recorded (green tick), in progress or next
// (grey). The states come from `~/lib/journey` (threadSteps / ziqiThreadSteps). Two columns in a
// narrow box (a 320px phone, a YoID dashboard card), four once the box is 32rem wide.
// ─────────────────────────────────────────────────────────────────────────────

const STATE_STYLES: Record<
  ThreadStep["state"],
  { dot: string; name: string; state: string }
> = {
  recorded: {
    dot: "bg-green text-white border-green",
    name: "text-black",
    state: "text-green",
  },
  "in progress": {
    dot: "bg-white text-blue-dark border-blue-dark",
    name: "text-black",
    // purple, not blue: blue-dark on white is under 4.5:1 at this size
    state: "text-purple font-semibold",
  },
  next: {
    dot: "bg-gray-light text-gray-dark border-gray",
    name: "text-gray-dark",
    state: "text-gray-dark",
  },
};

export const YoidThread: React.FC<{
  steps: ThreadStep[];
  yoid: string;
  /** Without the card frame (inside a card that already has one). */
  bare?: boolean;
  className?: string;
  /** Below the steps, e.g. a link to the listing. */
  children?: ReactNode;
}> = ({ steps, yoid, bare = false, className = "", children }) => {
  const labelId = useId();
  return (
    <div
      data-journey-thread=""
      className={`@container flex w-full min-w-0 flex-col gap-3 ${bare ? "" : "rounded-xl bg-white p-4 shadow-lg"} ${className}`}
    >
      <p
        id={labelId}
        className="text-gray-dark text-xs font-semibold break-words"
      >
        {threadLabel(yoid)}
      </p>
      <ol
        aria-labelledby={labelId}
        className="grid grid-cols-2 gap-x-3 gap-y-3 @lg:grid-cols-4"
      >
        {steps.map((step) => {
          const style = STATE_STYLES[step.state];
          return (
            <li
              key={step.phase}
              aria-current={step.state === "in progress" ? "step" : undefined}
              className="flex min-w-0 items-start gap-2"
            >
              <span
                aria-hidden="true"
                className={`mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full border-2 text-xs font-bold ${style.dot}`}
              >
                {step.state === "recorded" ? (
                  <IoCheckmark className="h-4 w-4" />
                ) : (
                  step.phase
                )}
              </span>
              <span className="flex min-w-0 flex-col">
                <span
                  className={`text-xs leading-tight font-semibold break-words md:text-sm ${style.name}`}
                >
                  {step.name}
                  <span className="sr-only">:</span>
                </span>
                <span className={`text-xs ${style.state}`}>{step.state}</span>
              </span>
            </li>
          );
        })}
      </ol>
      {children}
    </div>
  );
};

export default YoidThread;
