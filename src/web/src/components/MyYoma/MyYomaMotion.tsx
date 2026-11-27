import { useEffect, useState } from "react";

// ─────────────────────────────────────────────────────────────────────────────
// The first visit's motion, CSS only (no dependency): the YoID card flies in, slides slide in,
// chips pop, "Building your path" pulses. Every animation sits inside
// `prefers-reduced-motion: no-preference`, so with reduced motion everything appears in place.
// Class names are prefixed `myy-` so they never meet the rest of the app's CSS.
// ─────────────────────────────────────────────────────────────────────────────

const CSS = `
@media (prefers-reduced-motion: no-preference) {
  @keyframes myy-fly-in {
    0% { opacity: 0; transform: translateY(48px) rotate(-6deg) scale(0.86); }
    60% { opacity: 1; transform: translateY(-6px) rotate(1deg) scale(1.02); }
    100% { opacity: 1; transform: none; }
  }
  @keyframes myy-rise {
    from { opacity: 0; transform: translateY(14px); }
    to { opacity: 1; transform: none; }
  }
  @keyframes myy-slide-next {
    from { opacity: 0; transform: translateX(56px); }
    to { opacity: 1; transform: none; }
  }
  @keyframes myy-slide-prev {
    from { opacity: 0; transform: translateX(-56px); }
    to { opacity: 1; transform: none; }
  }
  @keyframes myy-pop-a {
    0% { transform: scale(1); }
    35% { transform: scale(1.07); }
    65% { transform: scale(0.97); }
    100% { transform: scale(1); }
  }
  @keyframes myy-pop-b {
    0% { transform: scale(1); }
    35% { transform: scale(1.07); }
    65% { transform: scale(0.97); }
    100% { transform: scale(1); }
  }
  @keyframes myy-dot {
    0%, 100% { transform: scale(0.6); opacity: 0.45; }
    50% { transform: scale(1); opacity: 1; }
  }
  @keyframes myy-draw {
    from { stroke-dashoffset: 1; }
    to { stroke-dashoffset: 0; }
  }
  .myy-fly-in { animation: myy-fly-in 700ms cubic-bezier(0.34, 1.4, 0.64, 1) both; }
  .myy-rise { animation: myy-rise 420ms ease-out both; }
  .myy-delay-1 { animation-delay: 350ms; }
  .myy-delay-2 { animation-delay: 600ms; }
  .myy-slide-next { animation: myy-slide-next 320ms cubic-bezier(0.2, 0.8, 0.2, 1) both; }
  .myy-slide-prev { animation: myy-slide-prev 320ms cubic-bezier(0.2, 0.8, 0.2, 1) both; }
  .myy-pop-a { animation: myy-pop-a 340ms cubic-bezier(0.34, 1.56, 0.64, 1); }
  .myy-pop-b { animation: myy-pop-b 340ms cubic-bezier(0.34, 1.56, 0.64, 1); }
  .myy-dot { animation: myy-dot 900ms ease-in-out infinite; }
  .myy-dot:nth-child(2) { animation-delay: 150ms; }
  .myy-dot:nth-child(3) { animation-delay: 300ms; }
  .myy-draw { stroke-dasharray: 1; animation: myy-draw 1400ms ease-in-out both; }
  .myy-progress { transition: width 300ms ease-out; }
}
`;

/** Rendered once by the first-visit flow; a plain style element, as the carousel does. */
export const MyYomaMotionStyles: React.FC = () => <style>{CSS}</style>;

/** `true` when the youth asked for reduced motion; `false` on the server and in old browsers. */
export const usePrefersReducedMotion = (): boolean => {
  const [reduced, setReduced] = useState(false);
  useEffect(() => {
    if (typeof window === "undefined" || !window.matchMedia) return;
    const query = window.matchMedia("(prefers-reduced-motion: reduce)");
    setReduced(query.matches);
    const onChange = (e: MediaQueryListEvent) => setReduced(e.matches);
    query.addEventListener?.("change", onChange);
    return () => query.removeEventListener?.("change", onChange);
  }, []);
  return reduced;
};
