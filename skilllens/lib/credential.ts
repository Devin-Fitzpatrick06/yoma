// Proposed pass rule for a skill credential in YoID (design, not built; INTEGRATION.md section 8).
export const PASS_LEVEL = 3;

/** Passed when every rubric criterion of the Level 2 answer is "Good" (3) or "Strong" (4). */
export const passed = (rows: { level: number }[]) => rows.length > 0 && rows.every((r) => r.level >= PASS_LEVEL);
