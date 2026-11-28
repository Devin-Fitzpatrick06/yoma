// Stand-in course pages for the demo. Each page says that it is demo data.
import { createServer } from "node:http";
import { courses } from "../seed/courses.mjs";
import { dayOffset, longDay } from "../seed/dates.mjs";
import { deadPages, listings, shared } from "../seed/journey.mjs";

const base = process.env.DEMO_BASE_DATE;
if (!base) throw new Error("DEMO_BASE_DATE is not set");

const escape = (text) => text.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);

function fill(line, course) {
  return line
    .replaceAll("{{start}}", longDay(dayOffset(base, course.startOffsetDays)))
    .replaceAll("{{end}}", longDay(dayOffset(base, course.endOffsetDays ?? course.startOffsetDays)))
    .replaceAll("{{laterStart}}", longDay(dayOffset(base, course.startOffsetDays + 30)))
    .replaceAll("{{laterEnd}}", longDay(dayOffset(base, (course.endOffsetDays ?? course.startOffsetDays) + 30)));
}

// A free course states price 0 in schema.org data, the same way a real provider's page would.
function structuredOffer(course) {
  if (!course.freeOffer) return "";
  const data = {
    "@context": "https://schema.org",
    "@type": course.freeOffer === "Event" ? "Event" : "Course",
    name: course.title,
    offers: { "@type": "Offer", price: "0", priceCurrency: "USD" },
  };
  return `<script type="application/ld+json">${JSON.stringify(data).replaceAll("<", "\\u003c")}</script>`;
}

function page(course) {
  const body = course.page.map((line) => `<p>${escape(fill(line, course))}</p>`).join("\n");
  return `<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>${escape(course.title)}</title>${structuredOffer(course)}</head>
<body style="font-family:system-ui,sans-serif;max-width:40rem;margin:2rem auto;padding:0 1rem;line-height:1.5">
<p style="background:#fff3cd;padding:.75rem;border-radius:.5rem">Demo data: a stand-in page for the Yoma Course Quality prototype, not a provider's real page.</p>
<h1>${escape(course.title)}</h1>
${body}
<p>It gives learners the details they look for on a real course page: what the topic covers, who it suits and how to take part. The wording was written for this prototype, so please do not treat it as a real offer from any organisation.</p>
</body>
</html>`;
}

// ---- The connected journey: one fictional page per shared listing, /journey/<shared id> ----
// The listing and the page come from the same file (seed/journey.mjs), so they agree. A page states its cost (free unless
// it says otherwise), its dates or that it is self-paced, what it covers, and schema.org data, as a provider's page would.
const naira = (n) => `₦${n.toLocaleString("en-GB")}`;
const skillLabel = (key) => (shared.skills[key] ?? key).replace(/\s*\(NEW KEY\)$/, "");
const languageNames = { en: "English", ha: "Hausa", yo: "Yoruba", pcm: "Nigerian Pidgin" };
const ldJson = (data) => `<script type="application/ld+json">${JSON.stringify({ "@context": "https://schema.org", ...data }).replaceAll("<", "\\u003c")}</script>`;
const longDate = (iso) => new Date(iso).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "Africa/Lagos" });
const clock = (iso) => iso.slice(11, 16);
const list = (items) => `<ul>${items.map((item) => `<li>${escape(item)}</li>`).join("")}</ul>`;

function journeyBody(listing) {
  const o = listing.shared;
  const org = listing.organization.name;
  if (listing.type === "Learning") {
    const langs = (o.language ?? ["en"]).map((l) => languageNames[l] ?? l).join(", ");
    return {
      ld: ldJson({ "@type": "Course", name: o.title, description: listing.summary, provider: { "@type": "Organization", name: org },
        inLanguage: o.language ?? ["en"], teaches: o.teaches.map(skillLabel), offers: { "@type": "Offer", price: "0", priceCurrency: "NGN" } }),
      html: `<p><strong>Provider:</strong> ${escape(org)}</p>
<p><strong>Cost:</strong> Free. There is nothing to pay.</p>
<p><strong>How it runs:</strong> Self-paced and ${escape(o.mode)}. Start anytime and learn at your own pace.</p>
<p><strong>Duration:</strong> about ${o.hours} hours</p>
<p><strong>Languages:</strong> ${escape(langs)}</p>
<h2>What you will learn</h2>
${list(listing.outcomes)}
<h2>Course outline</h2>
${list([`Module 1: the basics of ${skillLabel(o.teaches[0]).toLowerCase()}`, "Module 2: guided practice with short quizzes", "Module 3: a final check of what you have learned"])}
<h2>Who it suits</h2>
<p>Young people in Nigeria who want practical skills for work. No experience needed.</p>`,
    };
  }
  if (listing.type === "Job") {
    const fee = o.id === "opp-job-002";
    return {
      ld: ldJson({ "@type": "JobPosting", title: o.title, description: listing.summary, hiringOrganization: { "@type": "Organization", name: org },
        datePosted: listing.startIso.slice(0, 10), ...(listing.end ? { validThrough: listing.end } : {}), employmentType: fee ? "CONTRACTOR" : "FULL_TIME",
        jobLocation: { "@type": "Place", address: { "@type": "PostalAddress", addressLocality: o.location ?? "Kaduna", addressRegion: "Kaduna State", addressCountry: "NG" } },
        ...(o.payMonthlyNGN ? { baseSalary: { "@type": "MonetaryAmount", currency: "NGN", value: { "@type": "QuantitativeValue", value: o.payMonthlyNGN, unitText: "MONTH" } } } : {}) }),
      html: fee
        ? `<p><strong>Employer:</strong> ${escape(org)}</p>
<p><strong>Location:</strong> Kaduna, Kaduna State</p>
<p><strong>Pay:</strong> commission on every solar lamp and charger you sell.</p>
<p><strong>Registration fee:</strong> ${naira(5000)}. Pay the ${naira(5000)} registration fee before you start, to receive your agent ID and starter kit.</p>
<h2>How to apply</h2>
<p>Pay the registration fee, then send your name and phone number to start selling.</p>`
        : `<p><strong>Employer:</strong> ${escape(org)}</p>
<p><strong>Location:</strong> ${escape(o.location)}, Kaduna State (on site)</p>
<p><strong>Pay:</strong> ${naira(o.payMonthlyNGN)} a month</p>
<p><strong>Hours:</strong> ${escape(o.hours)} (full time)</p>
<p><strong>Cost to apply:</strong> free. We never ask applicants for money.</p>
<p>${escape(o.description)}</p>
<h2>You must have</h2>
${list(o.mustHave.map(skillLabel))}
<h2>You will learn on the job</h2>
${list(o.canLearn.map(skillLabel))}
<p>No diploma needed: we removed the old requirement (${escape(o.removedRequirement.toLowerCase())}).</p>
<h2>How to apply</h2>
<p>Apply through Yoma before ${escape(longDate(`${o.deadline}T12:00:00+01:00`))}.</p>`,
    };
  }
  // An event or an impact action: one day, with its time and place.
  const impact = listing.type === "ImpactAction";
  return {
    ld: ldJson({ "@type": "Event", name: o.title, description: listing.summary, startDate: listing.start, endDate: listing.end,
      eventAttendanceMode: "https://schema.org/OfflineEventAttendanceMode", organizer: { "@type": "Organization", name: org },
      location: { "@type": "Place", name: o.location, address: { "@type": "PostalAddress", addressLocality: "Kaduna", addressRegion: "Kaduna State", addressCountry: "NG" } },
      offers: { "@type": "Offer", price: "0", priceCurrency: "NGN" } }),
    html: `<p><strong>Organiser:</strong> ${escape(org)}</p>
<p><strong>Date:</strong> ${escape(longDate(listing.start))}</p>
<p><strong>Time:</strong> ${clock(listing.start)} to ${clock(listing.end)}</p>
<p><strong>Place:</strong> ${escape(o.location)}</p>
<p><strong>Cost:</strong> Free entry.</p>
${impact
    ? `<h2>What you will do</h2>
${list(["Plant seedlings with a local team", "Learn how to water and protect young trees", "Record your part with a photo"])}
<h2>Proof you took part</h2>
<p>Upload a photo taken on the day, with your phone's location switched on.</p>`
    : `<h2>What is on</h2>
${list(["Meet solar, recycling and agriculture employers", "Short talks on green skills", "Help with your CV"])}
<h2>Who it suits</h2>
<p>Young people in Kaduna looking for work in the green economy.</p>`}`,
  };
}

function journeyPage(listing) {
  const { ld, html } = journeyBody(listing);
  return `<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>${escape(listing.title)}</title>${ld}</head>
<body style="font-family:system-ui,sans-serif;max-width:40rem;margin:2rem auto;padding:0 1rem;line-height:1.5">
<p style="background:#fff3cd;padding:.75rem;border-radius:.5rem">Fictional sample page for the BBS prototypes (shared mock data v${escape(shared.meta.version)}). Not a real organisation, listing or offer.</p>
<h1>${escape(listing.title)}</h1>
${html}
</body>
</html>`;
}

const journeyPages = new Map(listings.filter((o) => !deadPages.has(o.sharedId)).map((o) => [`/journey/${o.sharedId}`, o]));

const pages = new Map(courses.filter((o) => !o.fixtureMissing).map((o) => [`/courses/${o.fixture}`, o]));

createServer((request, response) => {
  const path = new URL(request.url, "http://demo").pathname;
  const journey = journeyPages.get(path);
  if (journey) {
    response.writeHead(200, { "content-type": "text/html; charset=utf-8" });
    response.end(journeyPage({ ...journey, startIso: journey.start ?? `${dayOffset(base, journey.startOffsetDays ?? 0).toISOString()}` }));
    return;
  }
  const course = pages.get(path);
  response.writeHead(course ? 200 : 404, { "content-type": "text/html; charset=utf-8" });
  response.end(course ? page(course) : "<!doctype html><title>Page not found</title><h1>Page not found</h1><p>Demo data: this link is broken on purpose.</p>");
}).listen(80, "0.0.0.0", () => console.log("Demo fixture pages on port 80"));
