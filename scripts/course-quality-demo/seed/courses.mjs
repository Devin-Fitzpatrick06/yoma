// The one list of demo courses. generate-sql.mjs, assess.mjs, summary.mjs and fixtures/server.mjs all read it from here.
// demo-courses.json holds the organisations and the named courses 101 to 108 in full. catalogue.json holds the browse
// catalogue in short form, and each pattern below turns an entry into a full course that copies one named course.
import { readFileSync } from "node:fs";

const read = (name) => JSON.parse(readFileSync(new URL(name, import.meta.url)));

const named = read("./demo-courses.json");
const catalogue = read("./catalogue.json");

const slug = (title) => title.toLowerCase().replaceAll("'", "").replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

// Each page is worded so that the page reader lands on the expected state. README.md, "The catalogue", has the rules.
const patterns = {
  // like 102: the page's dates agree with the listing
  cohort: (c) => ({
    shows: "Verified, dates confirmed",
    freeOffer: c.free !== false || undefined,
    expect: { status: "Verified", datesConfirmed: true, cost: c.free === false ? "Unknown" : "Free" },
    page: [`About this course: ${c.about}`, "Start date: {{start}}", "End date: {{end}}", ...(c.free === false ? [] : ["This course is free for all learners."]), `The course covers ${c.detail}.`],
  }),
  // like 101: Alison's organisation on its configured domain, with no date anywhere on the page
  selfPaced: (c) => ({
    shows: "Verified, self-paced",
    organization: "alison",
    provider: "Alison Learning",
    freeOffer: true,
    endOffsetDays: null,
    expect: { status: "Verified", selfPaced: true, cost: "Free" },
    page: [c.about, "Self-paced. Start anytime and learn at your own speed.", `This free online course covers ${c.detail}.`],
  }),
  // like 106
  webinar: (c) => ({
    shows: "Webinar",
    freeOffer: "Event",
    endOffsetDays: c.startOffsetDays,
    expect: { status: "Verified", format: "Webinar", cost: "Free" },
    page: [`Join this webinar for a live conversation about ${c.detail}.`, "Date: {{start}}", "End date: {{start}}", "This webinar is free. Bring your questions for the panel of young professionals."],
  }),
  // like 103: a working page with no dates, and not a self-paced provider, so a person decides
  noDates: (c) => ({
    shows: "Not yet verified",
    freeOffer: c.free !== false || undefined,
    endOffsetDays: c.endOffsetDays ?? null,
    expect: { status: "NeedsReview", cost: c.free === false ? "Unknown" : "Free" },
    page: [`About the course: ${c.about}`, ...(c.free === false ? [] : ["This course is free."]), `It covers ${c.detail}.`],
  }),
  // like 108: the page's dates are 30 days later than the listing's
  datesDiffer: (c) => ({
    shows: "Pending review: page dates differ",
    expect: { status: "NeedsReview" },
    page: [`About this course: ${c.about}`, "Start date: {{laterStart}}", "End date: {{laterEnd}}", `The course covers ${c.detail}.`],
  }),
  // like 104
  paid: (c) => ({
    shows: "Paid cost",
    expect: { status: "NeedsReview", cost: "Paid" },
    page: [c.about, "Format: remote first.", `Course fee: ${c.fee}.`, "Enrol below to begin the course with the next group of learners."],
  }),
  // like 107: the link returns 404
  broken: (c) => ({
    shows: "Not verified, with a fix note",
    fixtureMissing: true,
    expect: { status: "NotVerified" },
  }),
};

function expand({ no, pattern, about, detail, fee, free, ...entry }) {
  const built = patterns[pattern]({ ...entry, about, detail, fee, free });
  const course = {
    id: `c0a7e000-0000-4000-8000-000000000${no}`,
    endOffsetDays: null,
    ...entry,
    ...built,
    fixture: `${slug(entry.title)}${built.fixtureMissing ? "-gone" : ""}`,
    summary: `Demo data: ${entry.summary}`,
  };
  return course;
}

export const organizations = named.organizations;
export const courses = [...named.courses.map((c) => ({ ...c, named: true })), ...catalogue.courses.map(expand)];

// Demo-only learners, so that view and completion counts differ between courses.
export const demoLearners = Array.from({ length: catalogue.demoLearners }, (_, i) => {
  const number = String(i + 1).padStart(2, "0");
  return { email: `demo.learner${number}@example.org`, surname: `Learner ${number}`, age: 18 + (i % 8), country: ["ZA", "US", "NG", "KE"][i % 4] };
});
