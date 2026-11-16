// Grades the whole catalogue once, for a start that loaded the snapshot and assessed only the named courses again.
import { runGrading } from "./api.mjs";

console.log(`  Grading: ${await runGrading()}`);
