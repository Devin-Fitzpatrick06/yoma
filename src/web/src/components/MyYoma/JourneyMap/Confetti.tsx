import styles from "./JourneyMap.module.css";

// ─────────────────────────────────────────────────────────────────────────────
// A short CSS confetti burst at a point of the map. Decorative: the map announces what finished in
// its live region. The caller removes it after `CONFETTI_MS` and never renders it under reduced
// motion.
// ─────────────────────────────────────────────────────────────────────────────

export const CONFETTI_MS = 1800;

const COLOURS = [
  "var(--color-orange)",
  "var(--color-green-dark)",
  "var(--color-purple-light)",
  "var(--color-blue)",
  "var(--color-pink)",
  "var(--color-yellow-light)",
];

const Confetti: React.FC<{ x: number; y: number; big?: boolean }> = ({
  x,
  y,
  big = false,
}) => {
  const count = big ? 30 : 16;
  const reach = big ? 120 : 72;
  return (
    <span
      aria-hidden="true"
      className="pointer-events-none absolute z-30"
      style={{ left: x, top: y }}
    >
      {Array.from({ length: count }, (_, i) => {
        // Even spread with a little deterministic jitter, so bursts look natural without Math.random.
        const angle = (i / count) * Math.PI * 2 + ((i * 37) % 11) / 20;
        const distance = reach * (0.55 + ((i * 53) % 9) / 18);
        const round = i % 3 === 0;
        return (
          <span
            key={i}
            className={`absolute top-0 left-0 block ${round ? "rounded-full" : "rounded-[1px]"} ${styles.confettiPiece}`}
            style={
              {
                width: round ? 7 : 5,
                height: round ? 7 : 10,
                background: COLOURS[i % COLOURS.length],
                "--tx": `${Math.cos(angle) * distance}px`,
                "--ty": `${Math.sin(angle) * distance - (big ? 24 : 12)}px`,
                "--rot": `${((i * 71) % 360) + 180}deg`,
                "--d": `${(i % 4) * 30}ms`,
              } as React.CSSProperties
            }
          />
        );
      })}
    </span>
  );
};

export default Confetti;
