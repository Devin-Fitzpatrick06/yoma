import Link from "next/link";
import { useRouter } from "next/router";
import { useState } from "react";
import { useEffect, useRef, type ReactNode } from "react";
import { env } from "~/env.mjs";
import {
  DEFAULT_DEMO_ORG_ID,
  demoSideOf,
  demoSides,
} from "~/lib/courseQualityDemo";

// ─────────────────────────────────────────────────────────────────────────────
// Course Quality demo only (NEXT_PUBLIC_COURSE_QUALITY_DEMO=true): a slim bar floating at the
// bottom of every page to move between the learner, reviewer and partner sides. The demo
// account is both an admin and a partner admin, so switching is only navigation. Renders
// nothing when the demo is off.
// ─────────────────────────────────────────────────────────────────────────────

export const COURSE_QUALITY_DEMO = env.NEXT_PUBLIC_COURSE_QUALITY_DEMO === true;

/**
 * Placement for a sticky action bar at the foot of a page. It clears the Help Scout support
 * bubble (fixed bottom right, about 60px with a 20px offset on phones and 40px from sm up) and,
 * in the demo, this switcher. Phones: the bar sits above the bubble. From sm up: it sits beside
 * it, with room on the right.
 */
export const STICKY_ACTION_BAR = COURSE_QUALITY_DEMO
  ? "bottom-[88px] sm:mr-[92px]"
  : "bottom-[84px] sm:bottom-0 sm:mr-[92px]";

const SIDES = demoSides(
  env.NEXT_PUBLIC_COURSE_QUALITY_DEMO_ORG_ID || DEFAULT_DEMO_ORG_ID,
);

/**
 * A short fade (160ms) when the page moves to another side. It runs once the new page has
 * mounted, starts from 40% rather than blank and holds nothing afterwards, so a page still
 * loading its data is never left washed out. Off with reduced motion and outside the demo.
 */
export const DemoPageFade: React.FC<{ children: ReactNode }> = ({
  children,
}) => {
  const router = useRouter();
  const ref = useRef<HTMLDivElement>(null);
  const side = demoSideOf(router.pathname) ?? "other";
  const previous = useRef(side);

  useEffect(() => {
    if (!COURSE_QUALITY_DEMO || previous.current === side) return;
    previous.current = side;
    const node = ref.current;
    if (
      !node?.animate ||
      window.matchMedia("(prefers-reduced-motion: reduce)").matches
    )
      return;
    const animation = node.animate([{ opacity: 0.4 }, { opacity: 1 }], {
      duration: 160,
      easing: "ease-out",
    });
    return () => animation.cancel();
  }, [side]);

  if (!COURSE_QUALITY_DEMO) return <>{children}</>;
  return <div ref={ref}>{children}</div>;
};

const DemoViewSwitcher: React.FC = () => {
  const router = useRouter();
  const [collapsed, setCollapsed] = useState(() =>
    router.pathname.startsWith("/my-yoma"),
  );
  if (!COURSE_QUALITY_DEMO) return null;

  const current = demoSideOf(router.pathname);
  const index = SIDES.findIndex((side) => side.side === current);
  const active = index >= 0 ? SIDES[index]! : null;

  // the journey map fills the screen, so there the bar starts as a small pill the presenter can open
  if (collapsed)
    return (
      <button
        type="button"
        onClick={() => setCollapsed(false)}
        className="fixed bottom-3 left-4 z-40 rounded-full bg-white px-3 py-1.5 text-xs font-semibold shadow-lg"
        aria-label="Open the demo view switcher"
      >
        Demo views
      </button>
    );

  return (
    <>
      {/* keeps the footer clear of the bar */}
      <div aria-hidden="true" className="h-20" />
      <nav
        aria-label="Demo views"
        className="fixed right-[84px] bottom-3 left-4 z-40 flex flex-col gap-1 rounded-2xl bg-white p-1.5 shadow-lg md:right-auto md:left-1/2 md:w-[calc(100%-2rem)] md:max-w-md md:-translate-x-1/2"
      >
        <p
          className="text-gray-dark truncate px-2 text-center text-xs"
          aria-live="polite"
        >
          {active ? (
            <>
              <span className="font-semibold text-black">
                {active.label} sees:
              </span>{" "}
              {active.caption}
            </>
          ) : (
            "Demo: switch between the three sides of Ziqi"
          )}
        </p>
        <div className="relative grid grid-cols-3">
          {/* the active indicator slides between the segments */}
          {active && (
            <span
              aria-hidden="true"
              className="bg-purple absolute inset-y-0 left-0 w-1/3 rounded-xl transition-transform duration-300 ease-out motion-reduce:transition-none"
              style={{ transform: `translateX(${index * 100}%)` }}
            />
          )}
          {SIDES.map((side) => {
            const selected = side.side === current;
            return (
              <Link
                key={side.side}
                href={side.href}
                aria-current={selected ? "page" : undefined}
                title={side.caption}
                className={`font-nunito focus-visible:outline-purple relative z-10 flex h-9 items-center justify-center rounded-xl text-sm font-black transition-colors duration-300 focus-visible:outline-2 focus-visible:outline-offset-2 ${
                  selected
                    ? "text-white"
                    : "text-gray-dark hover:bg-gray-light hover:text-black"
                }`}
              >
                {side.label}
              </Link>
            );
          })}
        </div>
      </nav>
    </>
  );
};

export default DemoViewSwitcher;
