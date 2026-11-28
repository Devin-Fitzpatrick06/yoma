# Link health check

Finds the dead links in a list of URLs: which links work, which have moved, which are dead, and which could not be confirmed.
It is built for any platform (Yoma, YouthHub, 1MiO, a partner's site or an intranet), and you do not need Yoma to run it.

There are two ways to run the same check:

- **The standalone CLI** (`check-links.mjs`): one file for Node 18 or later, with no npm packages and no install step.
  Anyone can run it on a laptop or a scheduled job.
- **The Yoma API endpoint** `POST /api/v3/courseQuality/links/check`, for Yoma Admins. It checks up to 200 links per call
  and uses the API's own safe link checker.

Both give the same statuses, and both use the same rules to spot a page that says it is gone and a bot wall
(`link-health-rules.json`).

## Running the CLI

```bash
# a text file: one URL per line (blank lines and lines starting with # are skipped)
node scripts/link-health/check-links.mjs links.txt --platform YouthHub --out report.csv --md report.md

# a CSV export: the url / link / URL column is found automatically, or name it
node scripts/link-health/check-links.mjs opportunities.csv --column "Course link" --out report.csv

# a sitemap: a URL or a local file (.xml or .xml.gz); a sitemap index is followed one level down
node scripts/link-health/check-links.mjs https://www.example.org/sitemap.xml --platform 1MiO --md report.md

# try it on the example list
node scripts/link-health/check-links.mjs scripts/link-health/examples/sample-urls.txt
```

Each platform exports its links as a .txt or .csv file, or points the check at its sitemap:

| Platform | Typical input |
| --- | --- |
| Yoma | an opportunities export (CSV with a URL column), or the list behind the admin link-health page |
| YouthHub | the site's `sitemap.xml`, or a CSV export of opportunity links |
| 1MiO | the site's `sitemap.xml`, or a .txt list of partner links |

| Option | Default | Meaning |
| --- | --- | --- |
| `--platform <name>` | none | label carried into the report |
| `--column <name or number>` | automatic | CSV column holding the links (1-based number, or header name) |
| `--concurrency <n>` | 4 | hosts checked at the same time |
| `--per-host-delay-ms <n>` | 1100 | least time between two requests to one host, so one site is never flooded |
| `--timeout-ms <n>` | 15000 | time allowed per link, with all its redirects |
| `--max-redirects <n>` | 5 | redirects followed per link |
| `--out <file.csv>` | none | CSV report: url, status, httpStatus, finalUrl, durationMs, reason, platform, dateChecked |
| `--md <file.md>` | none | Markdown report, grouped into dead, moved, could not confirm, not checked and working |
| `--json <file.json>` | none | the report in the API's `LinkHealthReport` shape |
| `--user-agent <text>` | a desktop browser's | User-Agent header |
| `--allow-private` | off | also check private/intranet addresses and any port: use only to check an intranet on purpose |
| `--proxy [url]` | off | connect through an HTTP proxy (with no URL: `HTTPS_PROXY` from the environment) |
| `--quiet` | off | print only the summary |

The exit code is **always 0**, so a scheduled job does not fail because some links are dead. Read the summary or the
report instead. Generated reports are not meant to be committed.

Behind a proxy that inspects TLS, also set `NODE_EXTRA_CA_CERTS=/path/to/proxy-ca.pem`.

## What the statuses mean

| Status | Counted as | Meaning |
| --- | --- | --- |
| `Ok` | working | The page loaded and looks like a real page. http→https, `www.`, letter case, a trailing slash and query changes do not count as a move. |
| `Redirected` | moved | The page loaded, but at a different site or path. Update the link. A deep link that now lands on the home page is flagged as possibly removed. |
| `NotFound` | **dead** | 404 or 410, or the page loads but says it is gone, removed, expired or not found (a "soft 404"). |
| `Broken` | **dead** | Any other 4xx or 5xx, a domain that does not exist, a connection or certificate (TLS) failure, too many redirects, or an empty page. |
| `Blocked` | could not confirm | Bot protection, a captcha, a sign-in wall, or 401/403/429. The page may work in a browser, so it is **not counted as dead**. A proxy that refuses the connection is reported here too. |
| `Timeout` | could not confirm | The site took longer than `--timeout-ms`. |
| `Invalid` | not checked | Not an http(s) address: no scheme, a typo such as `htp:`, `mailto:`, `ftp:`. |
| `Refused` | not checked | A private, loopback, link-local or cloud metadata address, a non-standard port, or a URL with a password in it. It is never opened. |

Each result also has a `reason`: a plain sentence a content editor can act on.

## Page rules (`link-health-rules.json`)

A page that answers `200 OK` can still be dead or walled. The CLI reads the page title, the first `<h1>` and the visible
text (scripts, styles, navigation and footers removed), and then applies these rules:

1. **Bot wall** (`Blocked`): a challenge header (`cf-mitigated`, `x-amzn-waf-action`), or words such as "Just a moment",
   "Checking your browser", "captcha" or "Client Challenge" in the title or heading. On a short page (under
   `shortPageLength` characters) the visible text counts too.
2. **Gone page** (`NotFound`): "not found", "404", "no longer available", "has been removed", "this vacancy has expired"
   and similar wording, in the title, the heading or the first `leadTextLength` characters. Only the opening of the page
   counts, so an article that mentions a 404 further down is not flagged.
3. **Sign-in wall** (`Blocked`): the final address is a `/login` or `/signin` page, the page is named "Sign in", or (on a
   short page only) it says you must sign in. A long page with a "log in to comment" box is a real page.

The Yoma API runs the same patterns: the gone and sign-in patterns come from the course quality assessment
(`CourseQualityPageMatch.cs`), and the bot-wall and expired patterns from `LinkHealthPageRules.cs`. An API unit test
(`CliRulesFile_MatchesTheApiPatterns`) fails if this file and the C# patterns drift apart, so change both together. The
patterns are case-insensitive regular expressions that work in both .NET and JavaScript.

## Safety

The URLs on the list may have been typed in by anyone, so the checker will not be turned into a way to reach internal
systems:

- Every hop, redirects included, is resolved with DNS first. If any address is private, loopback, link-local, carrier-grade
  NAT, multicast, reserved, or a known cloud metadata endpoint, the link is `Refused`. The address ranges are the API's
  (`CourseQualityAddressPolicy.cs`).
- Without a proxy, the connection is pinned to the addresses that were checked, so a second DNS answer cannot move it
  (DNS rebinding).
- Only ports 80 and 443 are checked, and a URL with a user name or password in it is never opened.
- Pages are only read, up to 1 MB each. Nothing on a page is followed or run, and nothing on it is copied into a reason.
- `--allow-private` opens private ranges and other ports, for checking your own intranet on purpose. Link-local and
  cloud metadata addresses stay refused even then.
- With `--proxy`, the proxy makes the connection. If this machine cannot resolve a name, the name is left to the proxy,
  and only literal addresses and local names (`localhost`, `*.local`, single-label names) can be refused up front.

## Using the Yoma API instead

Yoma Admins can call the endpoint directly. It takes at most 200 distinct URLs per call: an empty list, or more than 200
after duplicates are removed, gets a `400`. The endpoint works whether or not course quality is turned on, and it
stores nothing.

```bash
curl -X POST "https://<yoma-api-host>/api/v3/courseQuality/links/check" \
  -H "Authorization: Bearer <ADMIN_ACCESS_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
        "platform": "YouthHub",
        "urls": [
          "https://www.example.org/course/1",
          "https://www.example.org/old-page"
        ]
      }'
```

The response is a `LinkHealthReport`:

```json
{
  "platform": "YouthHub",
  "checked": 2,
  "counts": { "Ok": 1, "Redirected": 0, "NotFound": 1, "Broken": 0, "Blocked": 0, "Timeout": 0, "Invalid": 0, "Refused": 0 },
  "results": [
    { "url": "https://www.example.org/course/1", "status": "Ok", "httpStatus": 200, "finalUrl": "https://www.example.org/course/1", "durationMs": 412, "reason": "The page loaded." },
    { "url": "https://www.example.org/old-page", "status": "NotFound", "httpStatus": 404, "finalUrl": "https://www.example.org/old-page", "durationMs": 230, "reason": "The page does not exist (404)." }
  ],
  "dateChecked": "2026-11-28T10:00:00+00:00"
}
```

The API checks several hosts at a time (up to 8) and one host's links one after another, spaced by
`CourseQuality:LinkCheck:MinHostIntervalInMilliseconds`. Its timeout and redirect limits are the course quality link
checker's (`CourseQuality:LinkCheck`). The API identifies itself with the course quality bot's User-Agent, so a few sites
may answer it differently from the CLI.

## Tests

```bash
node --test scripts/link-health/test/check-links.test.mjs
```

The tests start a local server and check every status, the input formats, the reports, the refusals and the address policy.
