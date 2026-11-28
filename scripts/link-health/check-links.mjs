#!/usr/bin/env node
// Link health check for any platform (Yoma, YouthHub, 1MiO, an intranet, ...). Node 18 or later, no npm packages.
//
//   node check-links.mjs urls.txt --platform YouthHub --out report.csv --md report.md
//   node check-links.mjs export.csv --column "Course link"
//   node check-links.mjs https://example.org/sitemap.xml
//
// Statuses, the "gone page" and bot-wall rules match the Yoma API endpoint POST /api/v3/courseQuality/links/check;
// the page rules live in link-health-rules.json next to this file. See README.md.
//
// Safety: every hop is resolved first, and a target on a private, loopback, link-local or otherwise non-public address
// is refused unless --allow-private is given. Without a proxy the connection is pinned to the addresses that were
// checked, so a second DNS answer cannot move it. With --proxy, only a link given as input may be left to the proxy to
// resolve; a redirect target or a sitemap link must resolve here first. The exit code is always 0: read the report.

import { readFile, writeFile } from "node:fs/promises";
import { lookup } from "node:dns/promises";
import { fileURLToPath } from "node:url";
import http from "node:http";
import net from "node:net";
import path from "node:path";
import tls from "node:tls";
import zlib from "node:zlib";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const RULES = JSON.parse(await readFile(path.join(HERE, "link-health-rules.json"), "utf8"));

const STATUSES = ["Ok", "Redirected", "NotFound", "Broken", "Blocked", "Timeout", "Invalid", "Refused"];
const REDIRECTS = new Set([301, 302, 303, 307, 308]);
const DEFAULT_USER_AGENT =
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
const MAX_BODY_BYTES = 1_048_576;
const MAX_SITEMAP_BYTES = 20 * 1_048_576;
const MAX_CHILD_SITEMAPS = 50;

const rule = (source) => new RegExp(source, "i");
const PAGE = {
  softNotFound: rule(RULES.gone.softNotFound),
  expired: rule(RULES.gone.expired),
  botWall: rule(RULES.botWall.pattern),
  signInPath: rule(RULES.signIn.path),
  signInName: rule(RULES.signIn.name),
  signInWording: rule(RULES.signIn.wording),
};

// ── Arguments ────────────────────────────────────────────────────────────────

const HELP = `Link health check: reports which links work, moved, are dead or could not be confirmed.

Usage: node check-links.mjs <input> [options]

<input>  a .txt file (one URL per line; blank lines and lines starting with # are skipped),
         a .csv file (column url/link/URL found automatically, or --column),
         or a sitemap: a local .xml/.xml.gz file or an http(s) URL.

Options:
  --platform <name>         label carried into the report, e.g. YouthHub
  --column <name|number>    CSV column holding the links (name, or 1-based number)
  --concurrency <n>         hosts checked at the same time (default 4)
  --per-host-delay-ms <n>   least time between two requests to one host (default 1100)
  --timeout-ms <n>          time allowed per link, all redirects included (default 15000)
  --max-redirects <n>       redirects followed per link (default 5)
  --out <file.csv>          write a CSV report
  --md <file.md>            write a Markdown report
  --json <file.json>        write the report in the Yoma API's LinkHealthReport shape
  --user-agent <text>       User-Agent header (default: a desktop browser's)
  --allow-private           also check private/intranet addresses and any port (on purpose only)
  --proxy [url]             connect through an HTTP proxy (default: HTTPS_PROXY from the environment)
  --quiet                   print only the summary
  --help                    show this help

The exit code is always 0, so a scheduled job never fails because links are dead: read the report.`;

function parseArgs(argv) {
  const options = {
    input: null,
    platform: null,
    column: null,
    concurrency: 4,
    perHostDelayMs: 1100,
    timeoutMs: 15000,
    maxRedirects: 5,
    out: null,
    md: null,
    json: null,
    userAgent: DEFAULT_USER_AGENT,
    allowPrivate: false,
    proxy: null,
    quiet: false,
    help: false,
  };

  const number = (name, value, min) => {
    const parsed = Number(value);
    if (!Number.isInteger(parsed) || parsed < min) throw new Error(`${name} must be a whole number of at least ${min}`);
    return parsed;
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const value = () => {
      if (i + 1 >= argv.length) throw new Error(`${arg} needs a value`);
      return argv[++i];
    };

    switch (arg) {
      case "--help": case "-h": options.help = true; break;
      case "--platform": options.platform = value().trim() || null; break;
      case "--column": options.column = value(); break;
      case "--concurrency": options.concurrency = number(arg, value(), 1); break;
      case "--per-host-delay-ms": options.perHostDelayMs = number(arg, value(), 0); break;
      case "--timeout-ms": options.timeoutMs = number(arg, value(), 100); break;
      case "--max-redirects": options.maxRedirects = number(arg, value(), 0); break;
      case "--out": options.out = value(); break;
      case "--md": options.md = value(); break;
      case "--json": options.json = value(); break;
      case "--user-agent": options.userAgent = value(); break;
      case "--allow-private": options.allowPrivate = true; break;
      case "--quiet": options.quiet = true; break;
      case "--proxy":
        options.proxy = argv[i + 1] && !argv[i + 1].startsWith("--") ? argv[++i]
          : process.env.HTTPS_PROXY || process.env.https_proxy || process.env.HTTP_PROXY || process.env.http_proxy || null;
        if (!options.proxy) throw new Error("--proxy needs a URL, or HTTPS_PROXY in the environment");
        break;
      default:
        if (arg.startsWith("--")) throw new Error(`Unknown option ${arg} (see --help)`);
        if (options.input) throw new Error("Give one input file or sitemap URL");
        options.input = arg;
    }
  }

  if (options.proxy) options.proxy = new URL(options.proxy);
  return options;
}

// ── Address policy (mirrors CourseQualityAddressPolicy.cs) ───────────────────

const BLOCKED_RANGES = [
  ["0.0.0.0", 8], ["10.0.0.0", 8], ["100.64.0.0", 10], ["127.0.0.0", 8], ["168.63.129.16", 32], ["169.254.0.0", 16],
  ["172.16.0.0", 12], ["192.0.0.0", 24], ["192.0.2.0", 24], ["192.88.99.0", 24], ["192.168.0.0", 16], ["198.18.0.0", 15],
  ["198.51.100.0", 24], ["203.0.113.0", 24], ["224.0.0.0", 4], ["240.0.0.0", 4],
  ["::", 96], ["::ffff:0:0:0", 96], ["64:ff9b::", 96], ["64:ff9b:1::", 48], ["100::", 64], ["2001::", 32],
  ["2001:db8::", 32], ["2002::", 16], ["fc00::", 7], ["fe80::", 10], ["fec0::", 10], ["ff00::", 8],
].map(([network, prefix]) => [toBytes(network), prefix]);

function toBytes(address) {
  if (net.isIPv4(address)) return address.split(".").map(Number);
  if (!net.isIPv6(address)) return null;

  let value = address.toLowerCase();
  const v4 = value.match(/(\d+\.\d+\.\d+\.\d+)$/);
  if (v4) {
    const [a, b, c, d] = v4[1].split(".").map(Number);
    value = value.slice(0, -v4[1].length) + `${((a << 8) | b).toString(16)}:${((c << 8) | d).toString(16)}`;
  }

  const [head, tail] = value.includes("::") ? value.split("::") : [value, null];
  const left = head ? head.split(":") : [];
  const right = tail ? tail.split(":") : [];
  const groups = tail === null ? left : [...left, ...Array(8 - left.length - right.length).fill("0"), ...right];

  return groups.flatMap((group) => {
    const n = parseInt(group || "0", 16);
    return [n >> 8, n & 0xff];
  });
}

function inRange(bytes, network, prefix) {
  if (bytes.length !== network.length) return false;
  const whole = Math.floor(prefix / 8);
  for (let i = 0; i < whole; i++) if (bytes[i] !== network[i]) return false;
  const bits = prefix % 8;
  if (bits === 0) return true;
  const mask = (0xff << (8 - bits)) & 0xff;
  return (bytes[whole] & mask) === (network[whole] & mask);
}

export function isPublicAddress(address) {
  if (address.includes("%")) return false; // an IPv6 zone only exists on local addresses
  let bytes = toBytes(address);
  if (!bytes) return false;

  // IPv4-mapped IPv6 (::ffff:a.b.c.d) is judged as the IPv4 address
  if (bytes.length === 16 && bytes.slice(0, 10).every((b) => b === 0) && bytes[10] === 0xff && bytes[11] === 0xff) bytes = bytes.slice(12);

  return !BLOCKED_RANGES.some(([network, prefix]) => inRange(bytes, network, prefix));
}

// link-local and cloud metadata addresses are never an intranet page: refused even with --allow-private
const ALWAYS_BLOCKED = [
  ["169.254.0.0", 16], ["168.63.129.16", 32], ["100.100.100.200", 32], ["192.0.0.192", 32], ["fe80::", 10], ["fd00:ec2::254", 128],
].map(([network, prefix]) => [toBytes(network), prefix]);

export function isAllowedAddress(address, allowPrivate) {
  if (!allowPrivate) return isPublicAddress(address);
  let bytes = toBytes(address.split("%")[0]);
  if (!bytes) return false;
  if (bytes.length === 16 && bytes.slice(0, 10).every((b) => b === 0) && bytes[10] === 0xff && bytes[11] === 0xff) bytes = bytes.slice(12);
  return !ALWAYS_BLOCKED.some(([network, prefix]) => inRange(bytes, network, prefix));
}

function isLocalName(host) {
  const name = host.toLowerCase().replace(/\.$/, "");
  return !name.includes(".") || /(^|\.)(localhost|local|internal|intranet|lan|home\.arpa|corp)$/.test(name);
}

// ── Input ────────────────────────────────────────────────────────────────────

function parseCsv(text) {
  const rows = [];
  let row = [], cell = "", quoted = false;
  const header = text.slice(0, text.search(/\r?\n|$/));
  const delimiter = !header.includes(",") && header.includes(";") ? ";" : ","; // spreadsheets in many locales save with ;

  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (quoted) {
      if (ch === '"' && text[i + 1] === '"') { cell += '"'; i++; }
      else if (ch === '"') quoted = false;
      else cell += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === delimiter) { row.push(cell); cell = ""; }
    else if (ch === "\n" || ch === "\r") {
      if (ch === "\r" && text[i + 1] === "\n") i++;
      row.push(cell); rows.push(row); row = []; cell = "";
    } else cell += ch;
  }
  if (cell !== "" || row.length) { row.push(cell); rows.push(row); }

  return rows.filter((r) => r.some((c) => c.trim() !== ""));
}

function urlsFromCsv(text, column) {
  const rows = parseCsv(text.replace(/^\uFEFF/, ""));
  if (rows.length === 0) return [];

  const header = rows[0].map((h) => h.trim().toLowerCase());
  let index = -1;

  if (column != null) {
    index = /^\d+$/.test(column) ? Number(column) - 1 : header.indexOf(column.trim().toLowerCase());
    if (index < 0 || index >= header.length) throw new Error(`CSV column "${column}" not found; columns are: ${rows[0].join(", ")}`);
  } else {
    index = header.findIndex((h) => ["url", "link", "urls", "links", "href"].includes(h));
    if (index < 0) index = header.findIndex((h) => /\b(url|link)s?\b/.test(h));
    if (index < 0) {
      // no header: take the first column whose first row is a web address
      index = rows[0].findIndex((c) => /^https?:\/\//i.test(c.trim()));
      if (index >= 0) return rows.map((r) => r[index] ?? "");
      throw new Error(`No url or link column found; columns are: ${rows[0].join(", ")}. Use --column.`);
    }
  }

  return rows.slice(1).map((r) => r[index] ?? "");
}

const decodeXml = (value) => value
  .replace(/<!\[CDATA\[([\s\S]*?)\]\]>/g, "$1")
  .replace(/&lt;/g, "<").replace(/&gt;/g, ">").replace(/&quot;/g, '"').replace(/&apos;/g, "'")
  .replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n)))
  .replace(/&#x([0-9a-f]+);/gi, (_, n) => String.fromCodePoint(parseInt(n, 16)))
  .replace(/&amp;/g, "&");

function locs(xml) {
  return [...xml.matchAll(/<loc>\s*([\s\S]*?)\s*<\/loc>/gi)].map((m) => decodeXml(m[1]).trim());
}

function unzipIfNeeded(buffer) {
  return buffer.length > 2 && buffer[0] === 0x1f && buffer[1] === 0x8b ? zlib.gunzipSync(buffer, { maxOutputLength: MAX_SITEMAP_BYTES }) : buffer;
}

async function urlsFromSitemap(xml, options, depth = 0) {
  if (!/<sitemapindex[\s>]/i.test(xml)) return locs(xml);
  if (depth > 0) return []; // an index inside an index is not followed

  const children = locs(xml).slice(0, MAX_CHILD_SITEMAPS);
  const urls = [];
  for (const child of children) {
    try {
      urls.push(...await urlsFromSitemap(await fetchSitemap(child, options, true), options, depth + 1));
    } catch (error) {
      console.error(`Could not read the sitemap ${child}: ${error.message}`);
    }
  }
  return urls;
}

/** fromSitemap: a child sitemap named by an index, so chosen by the site rather than the user. */
async function fetchSitemap(url, options, fromSitemap = false) {
  const result = await checkLink(url, options, { wantBody: true, maxBytes: MAX_SITEMAP_BYTES, fromSitemap });
  if (!result.body) throw new Error(`${result.status}: ${result.reason}`);
  return unzipIfNeeded(result.body).toString("utf8");
}

/** The links to check, and whether they were read from a sitemap (so chosen by a site, not by the user). */
async function readInput(options) {
  const source = options.input;

  if (/^https?:\/\//i.test(source)) return { urls: await urlsFromSitemap(await fetchSitemap(source, options), options), fromSitemap: true };

  const buffer = unzipIfNeeded(await readFile(source));
  const text = buffer.toString("utf8").replace(/^\uFEFF/, "");
  const ext = path.extname(source.replace(/\.gz$/i, "")).toLowerCase();

  if (ext === ".csv") return { urls: urlsFromCsv(text, options.column), fromSitemap: false };
  if (ext === ".xml" || /^\s*<(\?xml|urlset|sitemapindex)/i.test(text)) return { urls: await urlsFromSitemap(text, options), fromSitemap: true };

  return { urls: text.split(/\r?\n/).map((line) => line.trim()).filter((line) => line && !line.startsWith("#")), fromSitemap: false };
}

function distinct(urls) {
  const seen = new Set();
  const result = [];
  for (const value of urls) {
    const url = String(value ?? "").trim();
    if (!url) continue;
    let key = url;
    try { if (/^[a-z][a-z0-9+.-]*:/i.test(url)) key = new URL(url).href; } catch { /* kept as written */ }
    if (!seen.has(key)) { seen.add(key); result.push(url); }
  }
  return result;
}

// ── Fetching ─────────────────────────────────────────────────────────────────

const nextSlotByHost = new Map();

async function paceHost(host, delayMs, signal) {
  if (delayMs <= 0) return;
  const now = Date.now();
  const next = nextSlotByHost.get(host) ?? now;
  const slot = Math.max(next, now);
  nextSlotByHost.set(host, slot + delayMs);
  if (slot > now) await sleep(slot - now, signal);
}

const sleep = (ms, signal) => new Promise((resolve, reject) => {
  const timer = setTimeout(resolve, ms);
  signal?.addEventListener("abort", () => { clearTimeout(timer); reject(signal.reason); }, { once: true });
});

/** The OS name lookup ignores cancellation, so the wait must not depend on it. */
const untilAborted = (promise, signal) => new Promise((resolve, reject) => {
  if (signal.aborted) return reject(signal.reason);
  const onAbort = () => reject(signal.reason);
  signal.addEventListener("abort", onAbort, { once: true });
  promise.then(resolve, reject).finally(() => signal.removeEventListener("abort", onAbort));
});

class CheckError extends Error {
  constructor(code, message, extra = {}) { super(message); this.code = code; Object.assign(this, extra); }
}

const bareHost = (name) => name.toLowerCase().replace(/\.$/, "").replace(/^www\./, "");
const sameSite = (a, b) => bareHost(a) === bareHost(b);

// With --proxy, a name this machine cannot resolve may be left to the proxy only for a link the user gave. A redirect hop or a
// link read from a sitemap is chosen by a site, which could point it at a name only the proxy's network knows.
const UNVERIFIED_REDIRECT = "could not be looked up on this machine, so the redirect target could not be verified locally and the redirect was not followed.";
const UNVERIFIED_SITEMAP_LINK = "came from a sitemap and could not be looked up on this machine, so it could not be verified locally and was not opened.";

/**
 * Resolves and checks one hop. Returns the addresses to pin, or null when the proxy will resolve the name.
 * origin: "input" (a link given to the CLI, hop 0), "redirect" (a later hop) or "sitemap" (a link read from a sitemap, hop 0).
 */
async function validateHop(url, options, origin = "input", inputHost = null) {
  const port = Number(url.port || (url.protocol === "https:" ? 443 : 80));
  const host = url.hostname.replace(/^\[|\]$/g, "");

  if (url.protocol !== "http:" && url.protocol !== "https:") throw new CheckError("REFUSED", "redirects to a non-web address, so the redirect was not followed.");
  if (url.username || url.password) throw new CheckError("REFUSED", "contains a user name or password, which would be sent to the site, so it was not opened.");
  if (!options.allowPrivate && port !== 80 && port !== 443) throw new CheckError("REFUSED", `uses port ${port}. Only the standard web ports (80 and 443) are checked; use --allow-private for an intranet.`);

  if (net.isIP(host)) {
    if (!isAllowedAddress(host, options.allowPrivate)) throw new CheckError("REFUSED", "points to a private, local or otherwise unsafe address, so it was not opened.");
    return { host, port, addresses: [host] };
  }

  if (!options.allowPrivate && isLocalName(host)) throw new CheckError("REFUSED", "points to a local or intranet name, so it was not opened.");

  let addresses;
  try {
    addresses = (await lookup(host, { all: true, verbatim: true })).map((a) => a.address);
  } catch (error) {
    // behind a proxy that resolves names itself, a name this machine cannot resolve is left to the proxy, for input links only
    if (options.proxy && ["ENOTFOUND", "EAI_AGAIN", "ESERVFAIL"].includes(error.code)) {
      // a redirect that stays on the host the user gave (http to https, another path, adding or dropping "www.") may go
      // to the proxy too: the user already chose to send that name to it
      if (origin === "redirect" && !(inputHost && sameSite(host, inputHost))) throw new CheckError("REFUSED", UNVERIFIED_REDIRECT, { unverified: true });
      if (origin === "sitemap") throw new CheckError("REFUSED", UNVERIFIED_SITEMAP_LINK, { unverified: true });
      return { host, port, addresses: null };
    }
    if (error.code === "ENOTFOUND" || error.code === "ENODATA") throw new CheckError("DNS", "The website's domain does not exist, so the link cannot open.");
    throw new CheckError("DNS", "The website's domain could not be looked up right now.");
  }

  // one internal answer is enough to refuse: a mixed result is how a rebinding host hides
  if (!addresses.every((address) => isAllowedAddress(address, options.allowPrivate))) throw new CheckError("REFUSED", "points to a private, local or otherwise unsafe address, so it was not opened.");

  return { host, port, addresses };
}

// an error after the step that waited for it (a reset while closing) must not crash the run: the waits report errors
const quiet = (emitter) => { emitter.on("error", () => {}); return emitter; };

function onceEvent(emitter, ok, signal) {
  return new Promise((resolve, reject) => {
    const cleanup = () => { emitter.off(ok, onOk); emitter.off("error", onError); signal?.removeEventListener("abort", onAbort); };
    const onOk = (...args) => { cleanup(); resolve(args); };
    const onError = (error) => { cleanup(); reject(error); };
    const onAbort = () => { cleanup(); emitter.destroy?.(); reject(signal.reason); };
    emitter.once(ok, onOk);
    emitter.once("error", onError);
    if (signal?.aborted) onAbort(); else signal?.addEventListener("abort", onAbort, { once: true });
  });
}

async function openSocket(hop, options, signal) {
  if (options.proxy) {
    const proxy = options.proxy;
    const target = `${net.isIPv6(hop.host) ? `[${hop.host}]` : hop.host}:${hop.port}`;
    const headers = { Host: target };
    if (proxy.username) headers["Proxy-Authorization"] = `Basic ${Buffer.from(`${decodeURIComponent(proxy.username)}:${decodeURIComponent(proxy.password)}`).toString("base64")}`;

    const request = quiet(http.request({ host: proxy.hostname, port: Number(proxy.port || 80), method: "CONNECT", path: target, headers, agent: false }));
    request.end();
    const [response, socket] = await onceEvent(request, "connect", signal);
    quiet(socket);
    if (response.statusCode !== 200) {
      socket.destroy();
      throw new CheckError("PROXY", `The proxy refused to connect to this site (${response.statusCode} from the proxy), so the site itself was not reached.`, { proxyStatus: response.statusCode });
    }
    return socket;
  }

  let lastError;
  for (const address of hop.addresses) {
    const socket = quiet(net.connect({ host: address, port: hop.port }));
    try {
      await onceEvent(socket, "connect", signal);
      return socket;
    } catch (error) {
      socket.destroy();
      if (signal.aborted) throw error;
      lastError = error;
    }
  }
  throw lastError;
}

async function sendRequest(url, hop, options, signal, maxBytes) {
  let socket = await openSocket(hop, options, signal);
  const onAbort = () => socket.destroy();
  signal.addEventListener("abort", onAbort, { once: true });

  if (url.protocol === "https:") {
    const secure = quiet(tls.connect({ socket, servername: net.isIP(hop.host) ? undefined : hop.host, ALPNProtocols: ["http/1.1"] }));
    await onceEvent(secure, "secureConnect", signal);
    socket = secure;
  }

  const hostHeader = (net.isIPv6(hop.host) ? `[${hop.host}]` : hop.host) + (url.port ? `:${url.port}` : "");
  const request = quiet(http.request({
    method: "GET",
    path: (url.pathname || "/") + url.search,
    headers: {
      Host: hostHeader,
      "User-Agent": options.userAgent,
      Accept: "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
      "Accept-Language": "en;q=0.9,*;q=0.5",
      "Accept-Encoding": "gzip, deflate, br",
      Connection: "close",
    },
    createConnection: () => socket, // no agent: this request uses exactly the socket opened above
  }));
  request.end();

  try {
    const [response] = await onceEvent(request, "response", signal);
    const status = response.statusCode;

    if (REDIRECTS.has(status)) {
      response.resume();
      return { status, headers: response.headers, body: null };
    }

    const body = await readBody(response, maxBytes, signal);
    return { status, headers: response.headers, body };
  } finally {
    signal.removeEventListener("abort", onAbort);
    socket.destroy();
  }
}

async function readBody(response, maxBytes, signal) {
  const encoding = String(response.headers["content-encoding"] || "").toLowerCase().trim();
  let stream = response;
  if (encoding === "gzip" || encoding === "x-gzip") stream = response.pipe(zlib.createGunzip());
  else if (encoding === "deflate") stream = response.pipe(zlib.createInflate());
  else if (encoding === "br") stream = response.pipe(zlib.createBrotliDecompress());

  const chunks = [];
  let total = 0;
  try {
    for await (const chunk of stream) {
      if (signal.aborted) break;
      chunks.push(chunk);
      total += chunk.length;
      if (total >= maxBytes) break;
    }
  } catch (error) {
    // headers said 2xx and some content arrived, so the page exists: keep what was read
    if (total === 0) throw error;
  } finally {
    stream.destroy();
    response.destroy();
  }
  if (signal.aborted && total === 0) throw signal.reason;
  return Buffer.concat(chunks).subarray(0, maxBytes);
}

// ── Reading a page (mirrors CourseQualityPageText / LinkHealthPageRules) ─────

const decodeHtml = (value) => value
  .replace(/&nbsp;/gi, " ").replace(/&lt;/gi, "<").replace(/&gt;/gi, ">").replace(/&quot;/gi, '"').replace(/&#39;|&apos;/gi, "'")
  .replace(/&#(\d+);/g, (_, n) => safeCodePoint(Number(n)))
  .replace(/&#x([0-9a-f]+);/gi, (_, n) => safeCodePoint(parseInt(n, 16)))
  .replace(/&amp;/gi, "&");

const safeCodePoint = (n) => (n > 0 && n <= 0x10ffff ? String.fromCodePoint(n) : " ");

function htmlToText(html) {
  return decodeHtml(html
    .replace(/<!--[\s\S]*?-->/g, " ")
    .replace(/<(script|style|noscript|template|svg|nav|footer)\b[^>]*>[\s\S]*?<\/\1\s*>/gi, " ")
    .replace(/<[^>]*>/g, " ")).replace(/\s+/g, " ").trim();
}

function innerText(html, tag) {
  const match = html.match(new RegExp(`<${tag}\\b[^>]*>([\\s\\S]*?)</${tag}\\s*>`, "i"));
  return match ? htmlToText(match[1]) : "";
}

function decodeBody(body, contentType) {
  const charset = /charset=["']?([\w-]+)/i.exec(contentType || "")?.[1];
  try {
    return new TextDecoder(charset || "utf-8").decode(body);
  } catch {
    return new TextDecoder("utf-8").decode(body);
  }
}

function readPage(body, contentType) {
  const media = String(contentType || "").split(";")[0].trim().toLowerCase();
  const isHtml = !media || media === "text/html" || media === "application/xhtml+xml";
  if (!isHtml && media !== "text/plain") return { title: "", heading: "", text: null };

  const content = decodeBody(body, contentType);
  if (!isHtml) return { title: "", heading: "", text: content.replace(/\s+/g, " ").trim() };

  return { title: innerText(content, "title"), heading: innerText(content, "h1"), text: htmlToText(content) };
}

function isBotWall({ title, heading, text }) {
  return PAGE.botWall.test(title) || PAGE.botWall.test(heading) || (text != null && text.length < RULES.shortPageLength && PAGE.botWall.test(text));
}

function isGone({ title, heading, text }) {
  const lead = (text ?? "").slice(0, RULES.leadTextLength);
  return [title, heading, lead].some((value) => PAGE.softNotFound.test(value) || PAGE.expired.test(value));
}

function isSignInWall({ title, heading, text }, finalUrl) {
  if (PAGE.signInPath.test(finalUrl.pathname) || PAGE.signInName.test(title) || PAGE.signInName.test(heading)) return true;
  return text != null && text.length < RULES.shortPageLength && PAGE.signInWording.test(text);
}

// ── Classification (mirrors LinkHealthService.Classify) ──────────────────────

const stripWww = (host) => host.toLowerCase().replace(/^www\./, "");
const pathOf = (url) => url.pathname.replace(/\/+$/, "").toLowerCase() || "/";

export function materiallyDifferent(requested, final) {
  return stripWww(requested.hostname) !== stripWww(final.hostname) || pathOf(requested) !== pathOf(final);
}

function classifyResponse(response, requested, final) {
  const status = response.status;
  const headers = response.headers;
  const moved = materiallyDifferent(requested, final);

  if (status === 404) return ["NotFound", moved ? "The link redirects to a page that does not exist (404)." : "The page does not exist (404)."];
  if (status === 410) return ["NotFound", "The site says this page has been removed for good (410)."];
  if (status === 401 || status === 403) return ["Blocked", `The site refused an automated check (${status}). It may work in a browser, so it is not counted as dead.`];
  if (RULES.botWallHeaders.some((name) => name in headers)) return ["Blocked", "The site's bot protection stopped the check. It may work in a browser, so it is not counted as dead."];
  if (status === 429) return ["Blocked", "The site asked the checker to slow down (429). Try again later; it is not counted as dead."];
  if (status >= 500) return ["Broken", `The site had an error (${status}). It may be down for now or for good.`];
  if (status < 200 || status > 299) return ["Broken", `The site answered with an error or an unusable response (${status}).`];
  if (!response.body || response.body.length === 0) return ["Broken", "The site answered with an empty page."];

  const page = readPage(response.body, headers["content-type"]);
  if (isBotWall(page)) return ["Blocked", "The site showed a bot check or captcha instead of the page. It may work in a browser, so it is not counted as dead."];
  if (isGone(page)) return ["NotFound", "The page loads, but says it is gone, removed, expired or cannot be found."];
  if (isSignInWall(page, final)) return ["Blocked", "The link leads to a sign-in page, so the page itself could not be checked. It is not counted as dead."];
  if (!moved) return ["Ok", "The page loaded."];
  if (pathOf(final) === "/" && pathOf(requested) !== "/") return ["Redirected", "The link now sends people to the site's home page; the page itself may have been removed."];
  return stripWww(requested.hostname) === stripWww(final.hostname)
    ? ["Redirected", "The page moved to another address on the same site. Update the link to the new address."]
    : ["Redirected", `The link now leads to another site (${final.hostname}). Check that it is still the right page.`];
}

function classifyError(error, requested, current) {
  if (error instanceof CheckError) {
    if (error.code === "REFUSED") {
      if (error.unverified) return ["Refused", error.message === UNVERIFIED_REDIRECT ? `The link redirects to an address that ${error.message}` : `The link ${error.message}`];
      const redirected = current !== requested && materiallyDifferent(requested, current);
      return ["Refused", redirected ? `The link redirects to an address that ${error.message.replace("so it was not opened.", "so the redirect was not followed.")}` : `The link ${error.message}`];
    }
    if (error.code === "PROXY") return ["Blocked", error.message];
    if (error.code === "TIMEOUT") return ["Timeout", "The site took too long to answer."];
    return ["Broken", error.message];
  }

  const code = String(error?.code || "");
  if (/CERT|SSL|TLS|SELF_SIGNED|UNABLE_TO_VERIFY|HOSTNAME|ALTNAME/i.test(code) || /certificate|ssl|tls/i.test(error?.message || ""))
    return ["Broken", "The site's security certificate is not valid, so a browser would warn people away."];
  if (["ECONNREFUSED", "ECONNRESET", "EHOSTUNREACH", "ENETUNREACH", "EPIPE", "ECONNABORTED", "HPE_INVALID_CONSTANT"].includes(code) || code.startsWith("HPE_"))
    return ["Broken", "Could not connect to the site: it is down or unreachable."];
  return ["Broken", `Could not connect to the site (${code || error?.message || "unknown error"}).`];
}

/**
 * Checks one link. Never throws: every outcome is a result. With wantBody the body is kept (for sitemaps).
 */
async function checkLink(raw, options, { wantBody = false, maxBytes = MAX_BODY_BYTES, fromSitemap = false } = {}) {
  const started = Date.now();
  const result = (status, reason, extra = {}) => ({ url: raw, status, httpStatus: null, finalUrl: null, durationMs: Date.now() - started, reason, ...extra });

  let requested;
  if (!/^[a-z][a-z0-9+.-]*:/i.test(raw)) return { ...result("Invalid", "This is not a full web address. It must start with http:// or https://."), durationMs: null };
  try { requested = new URL(raw); } catch { return { ...result("Invalid", "This is not a web address that can be checked."), durationMs: null }; }
  if (requested.protocol !== "http:" && requested.protocol !== "https:")
    return { ...result("Invalid", `The link starts with "${requested.protocol}", so it is not a web page (http or https).`), durationMs: null };
  if (!requested.hostname) return { ...result("Invalid", "The web address has no site name."), durationMs: null };

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(new CheckError("TIMEOUT", "timeout")), options.timeoutMs);
  let current = requested;
  let lastStatus = null;

  try {
    for (let hop = 0; hop <= options.maxRedirects; hop++) {
      const target = await untilAborted(validateHop(current, options, hop > 0 ? "redirect" : fromSitemap ? "sitemap" : "input", fromSitemap ? null : requested.hostname), controller.signal);
      await paceHost(current.hostname, options.perHostDelayMs, controller.signal);

      const response = await sendRequest(current, target, options, controller.signal, maxBytes);
      lastStatus = response.status;

      if (REDIRECTS.has(response.status)) {
        const location = response.headers.location;
        if (!location) return result("Broken", `The site answered with an error or an unusable response (${response.status}).`, { httpStatus: response.status, finalUrl: current.href });
        try { current = new URL(location, current); } catch {
          return result("Broken", "The link redirects to an address that cannot be read.", { httpStatus: response.status, finalUrl: current.href });
        }
        continue;
      }

      const [status, reason] = classifyResponse(response, requested, current);
      return result(status, reason, { httpStatus: response.status, finalUrl: current.href, ...(wantBody ? { body: response.body } : {}) });
    }

    return result("Broken", "The link redirects too many times, so it never reaches a page.", { httpStatus: lastStatus, finalUrl: current.href });
  } catch (raised) {
    const error = controller.signal.aborted && !(raised instanceof CheckError && raised.code !== "TIMEOUT") ? controller.signal.reason : raised;
    const [status, reason] = classifyError(error, requested, current);
    return result(status, reason, { finalUrl: status === "Refused" && current === requested ? null : current.href });
  } finally {
    clearTimeout(timer);
  }
}

// ── Scheduling ───────────────────────────────────────────────────────────────

async function checkAll(urls, options, onResult, { fromSitemap = false } = {}) {
  const results = new Array(urls.length);
  const groups = new Map();

  urls.forEach((url, index) => {
    let host = "";
    try { host = new URL(url).hostname.toLowerCase(); } catch { /* invalid: its own group */ }
    if (!groups.has(host)) groups.set(host, []);
    groups.get(host).push(index);
  });

  // hosts run in parallel; one host's links run one after another so the per-host delay spaces them
  const queue = [...groups.values()];
  const worker = async () => {
    for (let group = queue.shift(); group; group = queue.shift()) {
      for (const index of group) {
        results[index] = await checkLink(urls[index], options, { fromSitemap });
        onResult(results[index]);
      }
    }
  };
  await Promise.all(Array.from({ length: Math.min(options.concurrency, queue.length) }, worker));
  return results;
}

// ── Reports ──────────────────────────────────────────────────────────────────

function buildReport(results, platform) {
  const counts = Object.fromEntries(STATUSES.map((s) => [s, 0]));
  for (const r of results) counts[r.status]++;
  return { platform, checked: results.length, counts, results, dateChecked: new Date().toISOString() };
}

function csvCell(value) {
  let text = value == null ? "" : String(value);
  if (/^[=+\-@\t\r]/.test(text)) text = `'${text}`; // a spreadsheet must not run a cell as a formula
  return /[",\n\r]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function toCsv(report) {
  const header = ["url", "status", "httpStatus", "finalUrl", "durationMs", "reason", "platform", "dateChecked"];
  const lines = report.results.map((r) => [r.url, r.status, r.httpStatus, r.finalUrl, r.durationMs, r.reason, report.platform, report.dateChecked].map(csvCell).join(","));
  return [header.join(","), ...lines].join("\r\n") + "\r\n";
}

const MEANING = {
  Ok: "Loaded, and looks like a real page.",
  Redirected: "Loaded, but after moving to another address: update the link.",
  NotFound: "Dead: 404 or 410, or the page says it is gone, removed or expired.",
  Broken: "Dead or down: another error, a domain that does not exist, or a connection or certificate failure.",
  Blocked: "Could not confirm: bot protection, captcha, sign-in or rate limit. Not counted as dead; check in a browser.",
  Timeout: "Could not confirm: the site took too long.",
  Invalid: "Not a web address (http or https).",
  Refused: "Private, local or unsafe target: never opened.",
};

function md(value) {
  return String(value ?? "").replace(/\|/g, "\\|").replace(/[\r\n]+/g, " ");
}

function toMarkdown(report) {
  const lines = [
    `# Link health report${report.platform ? `: ${md(report.platform)}` : ""}`,
    "",
    `Checked ${report.checked} links on ${report.dateChecked}.`,
    "",
    "| Status | Links | Meaning |",
    "| --- | ---: | --- |",
    ...STATUSES.map((s) => `| ${s} | ${report.counts[s]} | ${MEANING[s]} |`),
    "",
  ];

  const section = (title, statuses) => {
    const rows = report.results.filter((r) => statuses.includes(r.status));
    if (rows.length === 0) return;
    lines.push(`## ${title} (${rows.length})`, "", "| Status | Link | HTTP | Final address | Reason |", "| --- | --- | ---: | --- | --- |");
    for (const r of rows) lines.push(`| ${r.status} | ${md(r.url)} | ${r.httpStatus ?? ""} | ${r.finalUrl && r.finalUrl !== r.url ? md(r.finalUrl) : ""} | ${md(r.reason)} |`);
    lines.push("");
  };

  section("Dead links: fix or remove", ["NotFound", "Broken"]);
  section("Moved: update the link", ["Redirected"]);
  section("Could not confirm: check in a browser", ["Blocked", "Timeout"]);
  section("Not checked", ["Invalid", "Refused"]);
  section("Working", ["Ok"]);

  return lines.join("\n");
}

function printSummary(report) {
  const dead = report.counts.NotFound + report.counts.Broken;
  const unconfirmed = report.counts.Blocked + report.counts.Timeout;
  console.log("");
  console.log(`Link health${report.platform ? ` for ${report.platform}` : ""}: ${report.checked} links checked`);
  for (const s of STATUSES) console.log(`  ${s.padEnd(10)} ${String(report.counts[s]).padStart(5)}`);
  console.log(`Dead: ${dead}. Moved: ${report.counts.Redirected}. Could not confirm (not counted as dead): ${unconfirmed}. Not checked: ${report.counts.Invalid + report.counts.Refused}.`);
}

// ── Main ─────────────────────────────────────────────────────────────────────

async function main() {
  let options;
  try {
    options = parseArgs(process.argv.slice(2));
  } catch (error) {
    console.error(error.message);
    console.error("Run with --help for usage.");
    return;
  }

  if (options.help || !options.input) {
    console.log(HELP);
    return;
  }

  let urls, fromSitemap;
  try {
    const input = await readInput(options);
    urls = distinct(input.urls);
    fromSitemap = input.fromSitemap;
  } catch (error) {
    console.error(`Could not read ${options.input}: ${error.message}`);
    return;
  }

  if (urls.length === 0) {
    console.error(`No links found in ${options.input}.`);
    return;
  }

  console.log(`Checking ${urls.length} distinct links (${options.concurrency} hosts at a time, ${options.perHostDelayMs} ms between requests to one host${options.proxy ? `, through proxy ${options.proxy.host}` : ""})...`);

  let done = 0;
  const results = await checkAll(urls, options, (r) => {
    done++;
    if (!options.quiet) console.log(`[${String(done).padStart(String(urls.length).length)}/${urls.length}] ${r.status.padEnd(10)} ${String(r.httpStatus ?? "-").padStart(3)}  ${r.url}  ${r.reason}`);
  }, { fromSitemap });

  const report = buildReport(results, options.platform);
  printSummary(report);

  const writes = [[options.out, () => toCsv(report)], [options.md, () => toMarkdown(report)], [options.json, () => JSON.stringify(report, null, 2) + "\n"]];
  for (const [file, render] of writes) {
    if (!file) continue;
    try {
      await writeFile(file, render(), "utf8");
      console.log(`Wrote ${file}`);
    } catch (error) {
      console.error(`Could not write ${file}: ${error.message}`);
    }
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => console.error(`Link health check stopped: ${error?.stack || error}`)).finally(() => { process.exitCode = 0; });
}
