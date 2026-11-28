// The connected journey's listings: the shared mock data v1.1 (../journey/shared-mock-data.json, fictional) mapped onto
// Yoma ids and fields. Read by generate-journey-sql.mjs (the listings) and fixtures/server.mjs (their pages), so a listing
// and its page always agree. The id mapping is the journey contract's (docs/work/active/course-quality-judger/
// journey-contract.md, "Ziqi's data mapping"); the fields Yoma needs and the shared file leaves out are Ziqi's own choices,
// listed in ../journey/README.md.
import { readFileSync } from "node:fs";

export const shared = JSON.parse(readFileSync(new URL("../journey/shared-mock-data.json", import.meta.url)));

const yomaId = (n) => `c0a7e000-0000-4000-8000-${String(n).padStart(12, "0")}`;
export const fixtureUrl = (sharedId) => `http://demo-fixtures.localtest.me/journey/${sharedId}`;
export const SAMPLE_NOTE = "Fictional sample data for the BBS prototypes";

export const organizationIds = {
  "org-sunrise": yomaId(11),
  "org-p2e": yomaId(12),
  "org-greenyouth": yomaId(13),
  "org-arewahub": yomaId(14),
  "org-fasttrack": yomaId(15),
};

export const opportunityIds = {
  "opp-job-001": yomaId(301),
  "opp-job-002": yomaId(302),
  "opp-course-001": yomaId(311),
  "opp-course-002": yomaId(312),
  "opp-course-003": yomaId(313),
  "opp-course-004": yomaId(314),
  "opp-course-005": yomaId(315),
  "opp-event-001": yomaId(321),
  "opp-impact-001": yomaId(331),
};

// The pages that are broken on purpose: the link checker gets 404 for these.
export const deadPages = new Set(["opp-course-005"]);

export const halima = {
  email: "halima.yusuf@example.org",
  first: shared.youngPerson.firstName,
  last: shared.youngPerson.lastName,
  yoid: shared.youngPerson.yoidId,
};

// Shared skill keys to existing Yoma skills (Lookup.Skill names). A key with no Yoma skill is left out.
const yomaSkills = {
  "solar-install": ["Photovoltaic Systems", "Solar Energy"],
  "work-at-height": ["Fall Protection", "Occupational Safety And Health"],
  customer: ["Customer Service"],
  comms: ["Communication"],
  teamwork: ["Teamwork"],
  "digital-basics": ["Digital Literacy"],
  "agri-practice": ["Agriculture", "Reforestation"],
};

const typeOf = { job: "Job", course: "Learning", event: "Event", "impact action": "ImpactAction" };
const languageNames = { en: "English", ha: "Hausa", yo: "Yoruba", pcm: "Nigerian Pidgin" };
const languageCodes = { en: "EN", ha: "HA", yo: "YO" }; // Pidgin has no Yoma language
const skillLabel = (key) => (shared.skills[key] ?? key).replace(/\s*\(NEW KEY\)$/, "");
const naira = (n) => `₦${n.toLocaleString("en-GB")}`;

// What the shared file does not say, per listing (see ../journey/README.md). Day offsets are from DEMO_BASE_DATE; fixed
// dates are the shared file's own, in West Africa Time (UTC+1).
const extras = {
  "opp-job-001": {
    category: "Engineering, Science & Mathematics", engagement: "OnSite", startOffsetDays: -10, end: "2027-01-15T23:59:59+01:00",
    summary: "Sample data: a paid solar trainee job in Kaduna North, no diploma needed.",
  },
  "opp-job-002": {
    category: "Business, Finance & Marketing", engagement: "OnSite", startOffsetDays: -10, end: null,
    summary: "Sample data: a solar sales agent role that asks for a registration fee.",
    description: "Sell solar lamps and phone chargers door to door in Kaduna and earn commission on every sale. To start, pay a ₦5,000 registration fee for your agent ID and starter kit. Location: Kaduna. Apply through the link.",
  },
  // outcomes: the "What you will learn" list on the course's page, as a complete provider page would show it
  "opp-course-001": {
    category: "Engineering, Science & Mathematics", summary: "Sample data: learn to install small solar PV systems safely.",
    outcomes: ["How a solar PV system works: panels, charge controller, battery and inverter", "Mount and connect panels safely",
      "Size a small system for a home or shop", "Explain the system and its care to a customer"],
  },
  "opp-course-002": {
    category: "Health, Safety & Wellbeing", summary: "Sample data: work safely on ladders and roofs.",
    outcomes: ["Check a ladder and set it up safely", "Use a harness and an anchor point", "Spot roof hazards before you climb"],
  },
  "opp-course-003": {
    category: "Personal Development & Career Readiness", summary: "Sample data: serve customers well, in person and by phone.",
    outcomes: ["Greet customers and listen to what they need", "Handle a complaint calmly", "Follow up with a customer by phone"],
  },
  "opp-course-004": {
    category: "Technology, AI & Data", summary: "Sample data: use a phone and computer with confidence, and stay safe online.",
    outcomes: ["Use a smartphone and a computer for everyday tasks", "Create strong passwords and spot scams online", "Search for information and fill in forms online"],
  },
  "opp-course-005": { category: "Engineering, Science & Mathematics", summary: "Sample data: an in-person wiring course whose link no longer works." },
  "opp-event-001": {
    category: "Personal Development & Career Readiness", engagement: "OnSite", start: "2026-12-12T10:00:00+01:00", end: "2026-12-12T16:00:00+01:00",
    commitment: [6, "Hour"], time: "10:00 to 16:00", summary: "Sample data: meet solar and green-economy employers in Kaduna.",
  },
  "opp-impact-001": {
    category: "Agriculture, Food, Environment and Climate", engagement: "OnSite", start: "2026-11-29T08:00:00+01:00", end: "2026-11-29T12:00:00+01:00",
    commitment: [4, "Hour"], time: "08:00 to 12:00", summary: "Sample data: plant trees with your community in Rigasa, Kaduna.",
  },
};

function description(o, org) {
  if (o.type === "job" && o.id === "opp-job-001") {
    const must = o.mustHave.map(skillLabel).join(", ").toLowerCase();
    const learn = o.canLearn.map(skillLabel).join(", ").toLowerCase();
    return `${o.description}\n\nPay: ${naira(o.payMonthlyNGN)} a month. Hours: ${o.hours}, full time. Location: ${o.location}, Kaduna State, on site. No experience needed.\nMust have: ${must}. You will learn on the job: ${learn}.\nHow to apply: apply through Yoma before ${longDate(o.deadline)}.`;
  }
  if (extras[o.id].description) return extras[o.id].description;
  if (o.type === "course") {
    const langs = (o.language ?? ["en"]).map((l) => languageNames[l] ?? l).join(", ");
    const teaches = o.teaches.map(skillLabel).join(", ").toLowerCase();
    const mode = o.mode === "in person" ? "an in-person course in Kaduna" : `a self-paced ${o.mode} course`;
    return `${o.title} is ${mode} from ${org.name}, about ${o.hours} hours in all. It teaches ${teaches}. Languages: ${langs}. Free to take.`;
  }
  if (o.type === "event") {
    return `A one-day careers fair for young people in Kaduna who want green jobs: meet solar, recycling and agriculture employers, join short skills talks and get help with your CV. ${longDate(o.date)}, ${extras[o.id].time}, at ${o.location}. Free entry.`;
  }
  return `Plant trees with ${org.name} in ${o.location} on ${longDate(o.date)}, ${extras[o.id].time}. Learn how to plant and care for seedlings, and work as a team. Proof: ${o.proofRequired.join(" and ")} (geolocation is your phone's location when you upload the photo).`;
}

function longDate(day) {
  return new Date(`${day}T12:00:00Z`).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });
}

export const organizations = shared.partners.map((p) => ({
  sharedId: p.id,
  id: organizationIds[p.id],
  name: p.name,
  type: p.type,
  partnerBadge: p.partnerBadge,
  tagline: SAMPLE_NOTE,
  biography: `${SAMPLE_NOTE}: ${p.name} is a made-up ${p.type} in Kaduna, not a real organisation.`,
}));

export const listings = shared.opportunities.map((o) => {
  const org = organizations.find((x) => x.sharedId === o.org);
  const extra = extras[o.id];
  const course = o.type === "course";
  return {
    sharedId: o.id,
    id: opportunityIds[o.id],
    type: typeOf[o.type],
    title: o.title,
    organization: org,
    summary: extra.summary,
    description: `${description(o, org)}\n\n${SAMPLE_NOTE}: not a real ${o.type === "impact action" ? "impact action" : o.type}.`,
    instructions: o.type === "job" ? "Open the link to read the full job advert, then apply." : "Open the link for the full details.",
    url: fixtureUrl(o.id),
    zlto: o.zlto ?? 0,
    category: extra.category,
    engagement: extra.engagement ?? (course && o.mode !== "in person" ? "Remote" : "OnSite"),
    commitment: course ? [o.hours, "Hour"] : (extra.commitment ?? null),
    outcomes: extra.outcomes ?? (o.teaches ?? []).map(skillLabel),
    startOffsetDays: extra.start ? null : (extra.startOffsetDays ?? -30),
    start: extra.start ?? null,
    end: extra.end ?? null,
    languages: [...new Set((o.language ?? ["en"]).map((l) => languageCodes[l]).filter(Boolean))],
    skills: [...new Set([...(o.teaches ?? []), ...(o.mustHave ?? []), ...(o.canLearn ?? [])].flatMap((k) => yomaSkills[k] ?? []))],
    shared: o,
  };
});
