import type {
  LinkHealthReport,
  LinkHealthRequest,
  LinkHealthResult,
} from "~/api/models/courseQuality";
import { countStatuses } from "./LinkHealth.helpers";

// Example link health for the fixture preview (`/ziqi-admin-preview`), without the API. The
// addresses are made up and nothing is fetched: the fake checker decides from the address alone.

const fakeResult = (url: string, index: number): LinkHealthResult => {
  const lower = url.toLowerCase();
  const base = { url, finalUrl: null, durationMs: 300 + ((index * 137) % 900) };
  if (!/^https?:\/\//.test(lower))
    return {
      ...base,
      status: "Invalid",
      httpStatus: null,
      durationMs: null,
      reason: "Not an http or https address.",
    };
  if (/localhost|127\.0\.0\.1|\/\/10\.|\/\/192\.168\./.test(lower))
    return {
      ...base,
      status: "Refused",
      httpStatus: null,
      durationMs: null,
      reason: "Private or local address: not fetched.",
    };
  if (lower.includes("signin") || lower.includes("linkedin"))
    return {
      ...base,
      status: "Blocked",
      httpStatus: 403,
      reason: "A sign-in page or bot check answered instead of the page.",
    };
  if (lower.includes("gone") || lower.includes("404"))
    return {
      ...base,
      status: "NotFound",
      httpStatus: 404,
      reason: "The site says this page does not exist.",
    };
  if (lower.includes("broken"))
    return {
      ...base,
      status: "Broken",
      httpStatus: 503,
      reason: "The server answered with an error (503).",
    };
  if (lower.includes("slow"))
    return {
      ...base,
      status: "Timeout",
      httpStatus: null,
      durationMs: 15_000,
      reason: "No answer within 15 seconds.",
    };
  if (lower.includes("old"))
    return {
      ...base,
      status: "Redirected",
      httpStatus: 200,
      finalUrl: url.replace(/old/i, "new"),
      reason: "Moved to a new address.",
    };
  return { ...base, status: "Ok", httpStatus: 200, reason: "Page loaded." };
};

export const LINK_HEALTH_FIXTURE_URLS = [
  "https://learn.example.org/courses/data-literacy",
  "https://learn.example.org/courses/old-intro-to-coding",
  "https://jobs.example.com/listing/gone-junior-technician",
  "https://events.example.net/broken/youth-summit",
  "https://www.linkedin.com/learning/example-course",
  "https://slow.example.org/webinar/renewable-energy",
  "http://192.168.1.20/admin",
  "ftp://files.example.org/brochure.pdf",
  "https://learn.example.org/courses/financial-basics",
];

/** Stands in for the API in the preview: a short wait, then a result per address. */
export const fakeLinkHealthCheck = async (
  request: LinkHealthRequest,
): Promise<LinkHealthReport> => {
  await new Promise((resolve) => setTimeout(resolve, 600));
  const results = request.urls.map(fakeResult);
  return {
    platform: request.platform ?? null,
    checked: results.length,
    counts: countStatuses(results),
    results,
    dateChecked: new Date().toISOString(),
  };
};

const fixtureResults = LINK_HEALTH_FIXTURE_URLS.map(fakeResult);

export const LINK_HEALTH_REPORT_FIXTURE: LinkHealthReport = {
  platform: "YouthHub (example)",
  checked: fixtureResults.length,
  counts: countStatuses(fixtureResults),
  results: fixtureResults,
  dateChecked: "2026-11-28T10:00:00Z",
};
