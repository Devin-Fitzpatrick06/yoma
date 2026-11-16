import type { CourseGrade } from "~/api/models/courseQuality";

// ─────────────────────────────────────────────────────────────────────────────
// A course's grade and its label, e.g. "A · Top pick". Neutral purple, never green: green marks
// verification only. Renders nothing for an ungraded course.
// ─────────────────────────────────────────────────────────────────────────────

const GradeChip: React.FC<{
  grade: CourseGrade | null | undefined;
  label?: string | null;
  size?: "xs" | "sm";
  /** White sits on a coloured band, like the card's format pill. */
  tone?: "tint" | "white";
  className?: string;
}> = ({ grade, label, size = "xs", tone = "tint", className = "" }) => {
  if (!grade || grade === "Ungraded") return null;
  return (
    <span
      className={`${tone === "white" ? "bg-white" : "bg-purple-tint"} text-purple inline-flex shrink-0 items-center gap-1 rounded-full font-bold whitespace-nowrap ${
        size === "sm" ? "px-2.5 py-1 text-sm" : "px-2 py-0.5 text-xs"
      } ${className}`}
    >
      <span className="sr-only">Grade: </span>
      <span className="font-nunito font-black">{grade}</span>
      {label && (
        <>
          <span aria-hidden="true">·</span>
          <span>{label}</span>
        </>
      )}
    </span>
  );
};

export default GradeChip;
