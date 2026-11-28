// Tests for check-links.mjs against a local server: node --test scripts/link-health/test/
// The server is on 127.0.0.1, so most cases run with --allow-private; the refusal cases run without it.
import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { mkdtemp, readFile, writeFile, rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import http from "node:http";
import os from "node:os";
import path from "node:path";
import zlib from "node:zlib";

import { isAllowedAddress, isPublicAddress, materiallyDifferent } from "../check-links.mjs";

const CLI = fileURLToPath(new URL("../check-links.mjs", import.meta.url));
const LONG = "<p>" + "Learn to clean, explore and chart data with spreadsheets and simple statistics. ".repeat(20) + "</p>";
const page = (title, body = LONG) => `<!doctype html><html><head><title>${title}</title></head><body><h1>${title}</h1>${body}</body></html>`;

let server, base, dir;

const routes = {
  "/ok": (res) => html(res, 200, page("Introduction to Data Analysis")),
  "/ok-gzip": (res) => {
    res.writeHead(200, { "content-type": "text/html; charset=utf-8", "content-encoding": "gzip" });
    res.end(zlib.gzipSync(page("Compressed course")));
  },
  "/mentions-sign-in": (res) => html(res, 200, page("Course", LONG + "<p>You must be logged in to post a comment.</p>")),
  "/404": (res) => html(res, 404, page("Not here")),
  "/410": (res) => html(res, 410, page("Gone")),
  "/500": (res) => html(res, 500, page("Error")),
  "/400": (res) => html(res, 400, page("Bad")),
  "/403": (res) => html(res, 403, page("Forbidden")),
  "/401": (res) => html(res, 401, page("Unauthorised")),
  "/429": (res) => html(res, 429, page("Slow down")),
  "/cf": (res) => { res.setHeader("cf-mitigated", "challenge"); html(res, 503, page("Wait")); },
  "/soft-404": (res) => html(res, 200, page("Page not found | Academy", "<p>Sorry, we could not find the page.</p>")),
  "/expired": (res) => html(res, 200, page("Graduate trainee", "<p>This vacancy has expired.</p>")),
  "/bot": (res) => html(res, 200, page("Just a moment...", "<p>Checking your browser before accessing the site.</p>")),
  "/captcha": (res) => html(res, 200, page("Security", "<p>Please complete the captcha to continue.</p>")),
  "/members": (res) => redirect(res, "/login?next=/members"),
  "/login": (res) => html(res, 200, page("Sign in | Academy", "<form>Email Password</form>")),
  "/moved": (res) => redirect(res, "/catalogue/new-place"),
  "/catalogue/new-place": (res) => html(res, 200, page("New place")),
  "/trailing": (res) => redirect(res, "/trailing/"),
  "/trailing/": (res) => html(res, 200, page("Trailing")),
  "/to-home": (res) => redirect(res, "/"),
  "/": (res) => html(res, 200, page("Home")),
  "/loop": (res) => redirect(res, "/loop"),
  "/to-metadata": (res) => redirect(res, "http://169.254.169.254/latest/meta-data/"),
  "/no-location": (res) => { res.writeHead(302); res.end(); },
  "/empty": (res) => { res.writeHead(200, { "content-type": "text/html" }); res.end(); },
  "/pdf": (res) => { res.writeHead(200, { "content-type": "application/pdf" }); res.end("%PDF-1.4 ..."); },
  "/slow": () => { /* never answers */ },
  "/sitemap.xml": (res) => {
    res.writeHead(200, { "content-type": "application/xml" });
    res.end(`<?xml version="1.0"?><urlset><url><loc>${base}/ok</loc></url><url><loc>${base}/404?a=1&amp;b=2</loc></url></urlset>`);
  },
};

function html(res, status, body) {
  res.writeHead(status, { "content-type": "text/html; charset=utf-8" });
  res.end(body);
}

function redirect(res, location) {
  res.writeHead(301, { location });
  res.end();
}

function run(args) {
  return new Promise((resolve) => {
    execFile(process.execPath, [CLI, ...args], { timeout: 60_000 }, (error, stdout, stderr) => resolve({ code: error?.code ?? 0, stdout, stderr }));
  });
}

async function check(urls, ...extra) {
  const input = path.join(dir, `in-${Math.random().toString(36).slice(2)}.txt`);
  const out = path.join(dir, `out-${Math.random().toString(36).slice(2)}.json`);
  await writeFile(input, urls.join("\n"));
  const result = await run([input, "--json", out, "--per-host-delay-ms", "0", "--timeout-ms", "1500", "--quiet", ...extra]);
  assert.equal(result.code, 0, result.stderr);
  return JSON.parse(await readFile(out, "utf8"));
}

before(async () => {
  dir = await mkdtemp(path.join(os.tmpdir(), "link-health-"));
  server = http.createServer((req, res) => {
    const route = routes[new URL(req.url, "http://x").pathname];
    if (route) route(res); else html(res, 404, page("Missing"));
  });
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  base = `http://127.0.0.1:${server.address().port}`;
});

after(async () => {
  server.closeAllConnections?.();
  server.close();
  await rm(dir, { recursive: true, force: true });
});

test("every status, the way the API names it", async () => {
  const expected = {
    "/ok": "Ok", "/ok-gzip": "Ok", "/mentions-sign-in": "Ok", "/trailing": "Ok", "/pdf": "Ok",
    "/moved": "Redirected", "/to-home": "Redirected",
    "/404": "NotFound", "/410": "NotFound", "/soft-404": "NotFound", "/expired": "NotFound",
    "/500": "Broken", "/400": "Broken", "/loop": "Broken", "/empty": "Broken", "/no-location": "Broken",
    "/403": "Blocked", "/401": "Blocked", "/429": "Blocked", "/cf": "Blocked", "/bot": "Blocked", "/captcha": "Blocked", "/members": "Blocked",
    "/slow": "Timeout",
    "/to-metadata": "Refused",
  };

  const report = await check(Object.keys(expected).map((p) => base + p), "--allow-private", "--concurrency", "1");

  for (const result of report.results) {
    const route = new URL(result.url).pathname;
    assert.equal(result.status, expected[route], `${route}: ${result.reason}`);
    assert.ok(result.reason.length > 0);
  }
  assert.equal(report.checked, Object.keys(expected).length);
  assert.equal(Object.values(report.counts).reduce((a, b) => a + b, 0), report.checked);
});

test("invalid input and duplicates", async () => {
  const report = await check(["not a link", "www.example.org/x", "mailto:a@example.org", "ftp://example.org/f", `${base}/ok`, `${base}/ok`], "--allow-private");

  assert.deepEqual(report.results.map((r) => r.status), ["Invalid", "Invalid", "Invalid", "Invalid", "Ok"]);
  assert.equal(report.checked, 5);
});

test("private, loopback and metadata targets are refused without --allow-private", async () => {
  const report = await check([`${base}/ok`, "http://127.0.0.1/", "http://10.0.0.1/", "http://[::1]/", "http://169.254.169.254/latest/meta-data/",
    "http://2130706433/", "http://intranet/", "http://printer.local/", "https://user:pass@example.org/"]);

  for (const result of report.results) assert.equal(result.status, "Refused", `${result.url}: ${result.reason}`);
});

test("sitemap and CSV input", async () => {
  const out = path.join(dir, "sitemap.json");
  await run([`${base}/sitemap.xml`, "--json", out, "--allow-private", "--per-host-delay-ms", "0", "--quiet"]);
  const fromSitemap = JSON.parse(await readFile(out, "utf8"));
  assert.deepEqual(fromSitemap.results.map((r) => [new URL(r.url).pathname + new URL(r.url).search, r.status]), [["/ok", "Ok"], ["/404?a=1&b=2", "NotFound"]]);

  const csv = path.join(dir, "links.csv");
  await writeFile(csv, `Title,Course Link\n"Data, basics",${base}/ok\nGone,${base}/410\n`);
  const csvOut = path.join(dir, "csv.json");
  await run([csv, "--json", csvOut, "--allow-private", "--per-host-delay-ms", "0", "--quiet", "--platform", "YouthHub"]);
  const fromCsv = JSON.parse(await readFile(csvOut, "utf8"));
  assert.equal(fromCsv.platform, "YouthHub");
  assert.deepEqual(fromCsv.results.map((r) => r.status), ["Ok", "NotFound"]);
});

test("CSV and Markdown reports are written, and a formula-looking cell is neutralised", async () => {
  const input = path.join(dir, "formula.txt");
  await writeFile(input, `${base}/ok\n=HYPERLINK("x")\n`);
  const csv = path.join(dir, "r.csv");
  const md = path.join(dir, "r.md");
  const result = await run([input, "--out", csv, "--md", md, "--allow-private", "--per-host-delay-ms", "0"]);

  assert.equal(result.code, 0);
  assert.match(result.stdout, /2 links checked/);
  const csvText = await readFile(csv, "utf8");
  assert.match(csvText, /^url,status,httpStatus,finalUrl,durationMs,reason,platform,dateChecked/);
  assert.match(csvText, /"'=HYPERLINK\(""x""\)",Invalid/);
  assert.match(await readFile(md, "utf8"), /\| Ok \| 1 \|/);
});

test("a missing input still exits 0", async () => {
  const result = await run([path.join(dir, "missing.txt")]);
  assert.equal(result.code, 0);
  assert.match(result.stderr, /Could not read/);
});

test("address policy matches the API's", () => {
  for (const a of ["127.0.0.1", "10.1.2.3", "172.16.0.1", "192.168.1.1", "169.254.169.254", "100.100.100.200", "168.63.129.16", "0.0.0.0",
    "224.0.0.1", "255.255.255.255", "::", "::1", "::ffff:127.0.0.1", "::ffff:169.254.169.254", "64:ff9b::7f00:1", "2002:7f00:1::", "fc00::1",
    "fd00:ec2::254", "fe80::1", "fe80::1%eth0", "ff02::1"]) assert.equal(isPublicAddress(a), false, a);
  for (const a of ["8.8.8.8", "172.32.0.1", "100.128.0.1", "2606:4700:4700::1111"]) assert.equal(isPublicAddress(a), true, a);

  // --allow-private opens the intranet, never link-local or cloud metadata addresses
  for (const a of ["10.1.2.3", "192.168.1.1", "127.0.0.1", "fd12::1"]) assert.equal(isAllowedAddress(a, true), true, a);
  for (const a of ["169.254.169.254", "::ffff:169.254.169.254", "100.100.100.200", "168.63.129.16", "fd00:ec2::254", "fe80::1%eth0"]) assert.equal(isAllowedAddress(a, true), false, a);
});

test("material redirects ignore trivia", () => {
  const d = (a, b) => materiallyDifferent(new URL(a), new URL(b));
  assert.equal(d("http://a.example.org/x", "https://www.a.example.org/X/"), false);
  assert.equal(d("https://a.example.org/x?id=1", "https://a.example.org/x?id=2"), false);
  assert.equal(d("https://a.example.org/x", "https://b.example.org/x"), true);
  assert.equal(d("https://a.example.org/x", "https://a.example.org/y"), true);
});
