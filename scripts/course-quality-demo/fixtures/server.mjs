// Stand-in course pages for the demo. Each page says that it is demo data.
import { readFileSync } from "node:fs";
import { createServer } from "node:http";
import { dayOffset, longDay } from "../seed/dates.mjs";

const { courses } = JSON.parse(readFileSync(new URL("../seed/demo-courses.json", import.meta.url)));
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

const pages = new Map(courses.filter((o) => !o.fixtureMissing).map((o) => [`/courses/${o.fixture}`, o]));

createServer((request, response) => {
  const course = pages.get(new URL(request.url, "http://demo").pathname);
  response.writeHead(course ? 200 : 404, { "content-type": "text/html; charset=utf-8" });
  response.end(course ? page(course) : "<!doctype html><title>Page not found</title><h1>Page not found</h1><p>Demo data: this link is broken on purpose.</p>");
}).listen(80, "0.0.0.0", () => console.log("Demo fixture pages on port 80"));
