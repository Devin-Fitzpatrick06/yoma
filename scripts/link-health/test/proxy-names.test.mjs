// With --proxy, a name this machine cannot resolve may be left to the proxy only for a link given as input. A redirect
// hop or a sitemap link must resolve here first. node --test scripts/link-health/test/
//
// A fake CONNECT proxy tunnels every name to one local server and records what it was asked for. The names end in
// ".example", which never resolves (RFC 2606), so this machine's lookup fails for all of them.
import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { mkdtemp, readFile, writeFile, rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import http from "node:http";
import net from "node:net";
import os from "node:os";
import path from "node:path";

const CLI = fileURLToPath(new URL("../check-links.mjs", import.meta.url));
const LONG = "<p>" + "Learn to clean, explore and chart data with spreadsheets and simple statistics. ".repeat(20) + "</p>";
const page = (title) => `<!doctype html><html><head><title>${title}</title></head><body><h1>${title}</h1>${LONG}</body></html>`;

let site, proxy, proxyUrl, dir;
const connects = [];

const routes = {
  "/ok": (res) => html(res, 200, page("Introduction to Data Analysis")),
  "/to-internal": (res) => redirect(res, "http://secret.internal-only.example/admin"),
  "/to-same-site": (res) => redirect(res, "/ok"),
  "/sitemap.xml": (res) => {
    res.writeHead(200, { "content-type": "application/xml" });
    res.end('<?xml version="1.0"?><urlset><url><loc>http://secret.internal-only.example/ok</loc></url></urlset>');
  },
  "/index.xml": (res) => {
    res.writeHead(200, { "content-type": "application/xml" });
    res.end('<?xml version="1.0"?><sitemapindex><sitemap><loc>http://secret.internal-only.example/sitemap.xml</loc></sitemap></sitemapindex>');
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

async function check(input, ...extra) {
  const out = path.join(dir, `out-${Math.random().toString(36).slice(2)}.json`);
  const result = await run([input, "--json", out, "--proxy", proxyUrl, "--per-host-delay-ms", "0", "--timeout-ms", "5000", "--quiet", ...extra]);
  assert.equal(result.code, 0, result.stderr);
  return JSON.parse(await readFile(out, "utf8"));
}

async function listFile(urls) {
  const file = path.join(dir, `in-${Math.random().toString(36).slice(2)}.txt`);
  await writeFile(file, urls.join("\n"));
  return file;
}

before(async () => {
  dir = await mkdtemp(path.join(os.tmpdir(), "link-health-proxy-"));

  site = http.createServer((req, res) => {
    const route = routes[new URL(req.url, "http://x").pathname];
    if (route) route(res); else html(res, 404, page("Missing"));
  });
  await new Promise((resolve) => site.listen(0, "127.0.0.1", resolve));
  const sitePort = site.address().port;

  // every CONNECT goes to the one site, whatever name it asks for
  proxy = http.createServer((_, res) => { res.writeHead(405); res.end(); });
  proxy.on("connect", (req, client, head) => {
    connects.push(req.url);
    const upstream = net.connect(sitePort, "127.0.0.1", () => {
      client.write("HTTP/1.1 200 Connection Established\r\n\r\n");
      if (head?.length) upstream.write(head);
      upstream.pipe(client);
      client.pipe(upstream);
    });
    upstream.on("error", () => client.destroy());
    client.on("error", () => upstream.destroy());
  });
  await new Promise((resolve) => proxy.listen(0, "127.0.0.1", resolve));
  proxyUrl = `http://127.0.0.1:${proxy.address().port}`;
});

after(async () => {
  site.closeAllConnections?.();
  site.close();
  proxy.closeAllConnections?.();
  proxy.close();
  await rm(dir, { recursive: true, force: true });
});

test("an input link this machine cannot resolve is still left to the proxy", async () => {
  connects.length = 0;
  const report = await check(await listFile(["http://courses.public-site.example/ok"]));

  assert.deepEqual(report.results.map((r) => r.status), ["Ok"]);
  assert.deepEqual(connects, ["courses.public-site.example:80"]);
});

test("a redirect to a name this machine cannot resolve is refused, and the proxy is never asked for it", async () => {
  connects.length = 0;
  const report = await check(await listFile(["http://courses.public-site.example/to-internal"]));

  const [result] = report.results;
  assert.equal(result.status, "Refused", result.reason);
  assert.match(result.reason, /redirect target could not be verified locally/);
  assert.equal(result.finalUrl, "http://secret.internal-only.example/admin");
  assert.deepEqual(connects, ["courses.public-site.example:80"]);
  assert.equal(report.counts.Refused, 1);
});

test("a redirect on the same unresolvable input host is refused too: every hop after the first must verify", async () => {
  connects.length = 0;
  const report = await check(await listFile(["http://courses.public-site.example/to-same-site"]));

  assert.equal(report.results[0].status, "Refused");
  assert.match(report.results[0].reason, /could not be verified locally/);
  assert.deepEqual(connects, ["courses.public-site.example:80"]);
});

test("a sitemap link this machine cannot resolve is refused, though the sitemap itself was an input", async () => {
  connects.length = 0;
  const report = await check("http://courses.public-site.example/sitemap.xml");

  assert.deepEqual(report.results.map((r) => [r.url, r.status]), [["http://secret.internal-only.example/ok", "Refused"]]);
  assert.match(report.results[0].reason, /came from a sitemap .* could not be verified locally/);
  assert.deepEqual(connects, ["courses.public-site.example:80"]);
});

test("a sitemap file's links are held to the same rule", async () => {
  connects.length = 0;
  const file = path.join(dir, "sitemap.xml");
  await writeFile(file, '<?xml version="1.0"?><urlset><url><loc>http://secret.internal-only.example/ok</loc></url></urlset>');

  const report = await check(file);

  assert.equal(report.results[0].status, "Refused");
  assert.deepEqual(connects, []);
});

test("a child sitemap named by an index must verify locally before it is fetched", async () => {
  connects.length = 0;
  const out = path.join(dir, "index.json");
  const result = await run(["http://courses.public-site.example/index.xml", "--json", out, "--proxy", proxyUrl, "--per-host-delay-ms", "0", "--timeout-ms", "5000", "--quiet"]);

  assert.match(result.stderr, /Could not read the sitemap http:\/\/secret\.internal-only\.example\/sitemap\.xml: Refused/);
  assert.deepEqual(connects, ["courses.public-site.example:80"]);
});
