import type {
  LinkHealthReport,
  LinkHealthResult,
  LinkHealthStatus,
} from "~/api/models/courseQuality";

// ─────────────────────────────────────────────────────────────────────────────
// Link health (Ziqi iteration 5): reading pasted or uploaded links, splitting them into the API's
// batches of 200, merging the reports, counting statuses and writing the CSV export. Pure, so
// `pnpm test` covers it without React or the API.
// ─────────────────────────────────────────────────────────────────────────────

/** The API checks at most this many URLs per request. */
export const LINK_HEALTH_BATCH_SIZE = 200;

/** In the order the results page lists them: problems first, then what could not be checked. */
export const LINK_HEALTH_STATUSES: LinkHealthStatus[] = [
  "NotFound",
  "Broken",
  "Timeout",
  "Blocked",
  "Refused",
  "Invalid",
  "Redirected",
  "Ok",
];

export const LINK_HEALTH_LABELS: Record<LinkHealthStatus, string> = {
  Ok: "Works",
  Redirected: "Redirected",
  NotFound: "Not found",
  Broken: "Broken",
  Blocked: "Blocked",
  Timeout: "Timed out",
  Invalid: "Not a web address",
  Refused: "Refused",
};

export const LINK_HEALTH_EXPLANATIONS: Record<LinkHealthStatus, string> = {
  Ok: "Loaded and looks like a real page.",
  Redirected: "Loaded after moving to another address. Update the link.",
  NotFound: "The page is gone (404 or 410, or it says so).",
  Broken: "An error page, or the site could not be reached.",
  Blocked:
    "Could not confirm: bot protection or a sign-in page. Check it by hand; it is not counted as dead.",
  Timeout: "The site took too long to answer.",
  Invalid: "Not an http or https address.",
  Refused:
    "Not checked: it points at a private, local or otherwise unsafe address.",
};

/** Colour plus text, never colour alone; text stays black on light tints for contrast. */
export const LINK_HEALTH_BADGES: Record<LinkHealthStatus, string> = {
  Ok: "bg-green-light text-green",
  Redirected: "bg-blue-light text-black",
  NotFound: "bg-orange-light text-black",
  Broken: "bg-orange-light text-black",
  Blocked: "bg-yellow-tint text-black",
  Timeout: "bg-yellow-tint text-black",
  Invalid: "bg-gray-light text-black",
  Refused: "bg-gray-light text-black",
};

/** Statuses that mean the link is dead or failing (Blocked and Refused are not). */
export const isProblemStatus = (status: LinkHealthStatus): boolean =>
  status === "NotFound" || status === "Broken" || status === "Timeout";

/** One link per line; blank lines and `#` comments dropped; duplicates kept once, in order. */
export const parseUrlList = (text: string): string[] =>
  uniqueUrls(
    text
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line && !line.startsWith("#")),
  );

/** Trims and drops empty and repeated links, keeping the first of each. */
export const uniqueUrls = (urls: Iterable<string>): string[] => {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const raw of urls) {
    const url = raw.trim();
    if (!url || seen.has(url)) continue;
    seen.add(url);
    out.push(url);
  }
  return out;
};

/**
 * A small RFC 4180 reader: commas (or the delimiter given), double-quoted fields with "" for a
 * quote, CRLF or LF rows. Fully empty rows are dropped.
 */
export const parseCsv = (text: string, delimiter = ","): string[][] => {
  const rows: string[][] = [];
  let row: string[] = [];
  let field = "";
  let quoted = false;
  const source = text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;

  const endRow = () => {
    row.push(field);
    if (row.some((cell) => cell.trim() !== "")) rows.push(row);
    row = [];
    field = "";
  };

  for (let i = 0; i < source.length; i++) {
    const char = source[i]!;
    if (quoted) {
      if (char === '"') {
        if (source[i + 1] === '"') {
          field += '"';
          i++;
        } else quoted = false;
      } else field += char;
    } else if (char === '"' && field === "") quoted = true;
    else if (char === delimiter) {
      row.push(field);
      field = "";
    } else if (char === "\n") endRow();
    else if (char === "\r") {
      if (source[i + 1] === "\n") i++;
      endRow();
    } else field += char;
  }
  if (field !== "" || row.length > 0) endRow();
  return rows;
};

/** Semicolon when the first line has more of them than commas (spreadsheet exports in some locales). */
export const detectDelimiter = (text: string): "," | ";" | "\t" => {
  const firstLine = text.split(/\r?\n/, 1)[0] ?? "";
  const count = (char: string) => firstLine.split(char).length - 1;
  const tabs = count("\t");
  const semicolons = count(";");
  const commas = count(",");
  if (tabs > commas && tabs > semicolons) return "\t";
  return semicolons > commas ? ";" : ",";
};

const looksLikeUrl = (value: string): boolean =>
  /^(https?:\/\/|www\.)/i.test(value.trim());

/** A first row with no link in it is a header row. */
export const hasHeaderRow = (rows: string[][]): boolean =>
  rows.length > 0 && !rows[0]!.some(looksLikeUrl);

/**
 * The column most likely to hold the links: a header named url/link/href (or containing it),
 * else the column whose cells most often look like links. -1 when nothing fits.
 */
export const findUrlColumn = (rows: string[][]): number => {
  if (rows.length === 0) return -1;
  if (hasHeaderRow(rows)) {
    const headers = rows[0]!.map((cell) => cell.trim().toLowerCase());
    const exact = headers.findIndex((h) =>
      ["url", "link", "href", "website", "address"].includes(h),
    );
    if (exact >= 0) return exact;
    const partial = headers.findIndex((h) => /url|link/.test(h));
    if (partial >= 0) return partial;
  }
  const width = Math.max(...rows.map((row) => row.length));
  let best = -1;
  let bestCount = 0;
  for (let column = 0; column < width; column++) {
    const count = rows.filter((row) => looksLikeUrl(row[column] ?? "")).length;
    if (count > bestCount) {
      best = column;
      bestCount = count;
    }
  }
  return best;
};

/** The names to offer for each column: its header, or "Column 1", "Column 2", … */
export const columnNames = (rows: string[][]): string[] => {
  if (rows.length === 0) return [];
  const width = Math.max(...rows.map((row) => row.length));
  const header = hasHeaderRow(rows) ? rows[0]! : [];
  return Array.from(
    { length: width },
    (_, i) => header[i]?.trim() || `Column ${i + 1}`,
  );
};

/** The links in one column, without the header row. */
export const columnUrls = (rows: string[][], column: number): string[] =>
  column < 0
    ? []
    : uniqueUrls(
        (hasHeaderRow(rows) ? rows.slice(1) : rows).map(
          (row) => row[column] ?? "",
        ),
      );

/** Splits into the API's batches, in order. */
export const toBatches = <T>(
  items: readonly T[],
  size = LINK_HEALTH_BATCH_SIZE,
): T[][] => {
  const step = Math.max(1, Math.floor(size));
  const batches: T[][] = [];
  for (let i = 0; i < items.length; i += step)
    batches.push(items.slice(i, i + step));
  return batches;
};

/** Every status, zero when absent. */
export const emptyCounts = (): Record<LinkHealthStatus, number> =>
  Object.fromEntries(LINK_HEALTH_STATUSES.map((s) => [s, 0])) as Record<
    LinkHealthStatus,
    number
  >;

export const countStatuses = (
  results: readonly LinkHealthResult[],
): Record<LinkHealthStatus, number> => {
  const counts = emptyCounts();
  for (const result of results)
    if (result.status in counts) counts[result.status]++;
  return counts;
};

/** One report from the batches' reports, counted again from the results. */
export const mergeReports = (
  reports: readonly LinkHealthReport[],
  platform: string | null,
): LinkHealthReport | null => {
  if (reports.length === 0) return null;
  const results = reports.flatMap((report) => report.results);
  return {
    platform: platform ?? reports[0]!.platform ?? null,
    checked: results.length,
    counts: countStatuses(results),
    results,
    dateChecked: reports[reports.length - 1]!.dateChecked,
  };
};

const csvCell = (value: string | number | null | undefined): string => {
  const text = value == null ? "" : String(value);
  // a leading =, +, - or @ would run as a formula in a spreadsheet
  const safe = /^[=+\-@\t\r]/.test(text) ? `'${text}` : text;
  return /[",\r\n]/.test(safe) ? `"${safe.replace(/"/g, '""')}"` : safe;
};

export const LINK_HEALTH_CSV_HEADER = [
  "url",
  "status",
  "status_label",
  "http_status",
  "final_url",
  "duration_ms",
  "reason",
  "platform",
  "date_checked",
];

/** The report as CSV, one row per link, CRLF line ends. */
export const linkHealthToCsv = (report: LinkHealthReport): string =>
  [
    LINK_HEALTH_CSV_HEADER.join(","),
    ...report.results.map((result) =>
      [
        result.url,
        result.status,
        LINK_HEALTH_LABELS[result.status] ?? result.status,
        result.httpStatus,
        result.finalUrl,
        result.durationMs,
        result.reason,
        report.platform,
        report.dateChecked,
      ]
        .map(csvCell)
        .join(","),
    ),
  ].join("\r\n") + "\r\n";

/** "link-health-youthhub-2026-11-28.csv" */
export const linkHealthFileName = (
  platform: string | null,
  date: Date,
): string => {
  const slug = (platform ?? "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 40);
  const day = date.toISOString().slice(0, 10);
  return `link-health-${slug ? `${slug}-` : ""}${day}.csv`;
};
