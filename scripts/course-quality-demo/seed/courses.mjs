// The named walkthrough courses 101 to 108. generate-sql.mjs, assess.mjs, summary.mjs and fixtures/server.mjs read them
// from here. demo-courses.json holds them in full. Every other course in the demo is a live Yoma listing (see live.mjs).
import { readFileSync } from "node:fs";

const named = JSON.parse(readFileSync(new URL("./demo-courses.json", import.meta.url)));

// "live" organisations come from the live catalogue import, so only the others are inserted from here.
export const organizations = named.organizations;
export const courses = named.courses.map((c) => ({ ...c, named: true }));

// Demo-only learners, so that view, start and completion counts differ between courses. They have no password.
export const demoLearners = Array.from({ length: 50 }, (_, i) => {
  const number = String(i + 1).padStart(2, "0");
  return { email: `demo.learner${number}@example.org`, surname: `Learner ${number}`, age: 18 + (i % 8), country: ["ZA", "US", "NG", "KE", "PH", "IN"][i % 6] };
});
