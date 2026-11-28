import assert from "node:assert/strict";
import { describe, test } from "node:test";
import type {
  LinkHealthReport,
  LinkHealthResult,
} from "~/api/models/courseQuality";
import {
  columnNames,
  columnUrls,
  countStatuses,
  detectDelimiter,
  findUrlColumn,
  hasHeaderRow,
  isProblemStatus,
  LINK_HEALTH_CSV_HEADER,
  LINK_HEALTH_STATUSES,
  linkHealthFileName,
  linkHealthToCsv,
  mergeReports,
  parseCsv,
  parseUrlList,
  toBatches,
} from "./LinkHealth.helpers";

const result = (
  url: string,
  status: LinkHealthResult["status"],
  extra: Partial<LinkHealthResult> = {},
): LinkHealthResult => ({
  url,
  status,
  httpStatus: null,
  finalUrl: null,
  durationMs: null,
  reason: "",
  ...extra,
});

describe("parseUrlList", () => {
  test("one per line, trimmed, blanks and comments dropped, duplicates once", () => {
    assert.deepEqual(
      parseUrlList(
        "  https://a.org/1 \r\n\n# a note\nhttps://b.org/2\nhttps://a.org/1\n",
      ),
      ["https://a.org/1", "https://b.org/2"],
    );
  });
});

describe("parseCsv", () => {
  test("quoted fields, escaped quotes, commas and newlines inside quotes", () => {
    assert.deepEqual(
      parseCsv(
        'title,url\r\n"Course, one","https://a.org/1"\n"Say ""hi""\nthere",https://b.org\n',
      ),
      [
        ["title", "url"],
        ["Course, one", "https://a.org/1"],
        ['Say "hi"\nthere', "https://b.org"],
      ],
    );
  });

  test("drops empty rows and a byte-order mark, keeps a last row without a newline", () => {
    assert.deepEqual(parseCsv("﻿url\n\n,\nhttps://a.org"), [
      ["url"],
      ["https://a.org"],
    ]);
  });

  test("semicolons and tabs as delimiters", () => {
    assert.equal(detectDelimiter("name;url;notes\n"), ";");
    assert.equal(detectDelimiter("name\turl\n"), "\t");
    assert.equal(detectDelimiter("name,url\n"), ",");
    assert.deepEqual(parseCsv("a;https://x.org", ";"), [
      ["a", "https://x.org"],
    ]);
  });
});

describe("the link column", () => {
  test("a header named url or link wins", () => {
    const rows = parseCsv("Title,Provider,Course link\nA,P,https://a.org\n");
    assert.equal(hasHeaderRow(rows), true);
    assert.equal(findUrlColumn(rows), 2);
    assert.deepEqual(columnNames(rows), ["Title", "Provider", "Course link"]);
    assert.deepEqual(columnUrls(rows, 2), ["https://a.org"]);
  });

  test("without a header, the column that looks most like links", () => {
    const rows = parseCsv("A,https://a.org\nB,https://b.org\nC,\n");
    assert.equal(hasHeaderRow(rows), false);
    assert.equal(findUrlColumn(rows), 1);
    assert.deepEqual(columnNames(rows), ["Column 1", "Column 2"]);
    assert.deepEqual(columnUrls(rows, 1), ["https://a.org", "https://b.org"]);
  });

  test("nothing that looks like a link is -1, and -1 gives no links", () => {
    const rows = parseCsv("name,notes\nA,B\n");
    assert.equal(findUrlColumn(rows), -1);
    assert.deepEqual(columnUrls(rows, -1), []);
  });
});

describe("toBatches", () => {
  test("batches of 200 by default, in order, the last one shorter", () => {
    const urls = Array.from({ length: 450 }, (_, i) => `https://x.org/${i}`);
    const batches = toBatches(urls);
    assert.deepEqual(
      batches.map((batch) => batch.length),
      [200, 200, 50],
    );
    assert.equal(batches[2]![0], "https://x.org/400");
    assert.deepEqual(toBatches([]), []);
    assert.deepEqual(toBatches([1, 2, 3], 0), [[1], [2], [3]]);
  });
});

describe("counts and merging", () => {
  test("every status is counted, zero when absent", () => {
    const counts = countStatuses([
      result("a", "Ok"),
      result("b", "Ok"),
      result("c", "Blocked"),
    ]);
    assert.equal(counts.Ok, 2);
    assert.equal(counts.Blocked, 1);
    assert.equal(counts.NotFound, 0);
    assert.equal(Object.keys(counts).length, LINK_HEALTH_STATUSES.length);
  });

  test("Blocked and Refused are not counted as dead links", () => {
    assert.equal(isProblemStatus("Blocked"), false);
    assert.equal(isProblemStatus("Refused"), false);
    assert.equal(isProblemStatus("NotFound"), true);
  });

  test("batches merge into one report, counted again from the results", () => {
    const one: LinkHealthReport = {
      platform: "YouthHub",
      checked: 1,
      counts: countStatuses([result("a", "Ok")]),
      results: [result("a", "Ok")],
      dateChecked: "2026-11-28T10:00:00Z",
    };
    const two: LinkHealthReport = {
      ...one,
      results: [result("b", "NotFound"), result("c", "Refused")],
      dateChecked: "2026-11-28T10:05:00Z",
    };
    const merged = mergeReports([one, two], "YouthHub")!;
    assert.equal(merged.checked, 3);
    assert.equal(merged.counts.NotFound, 1);
    assert.equal(merged.counts.Refused, 1);
    assert.equal(merged.dateChecked, "2026-11-28T10:05:00Z");
    assert.equal(mergeReports([], null), null);
  });
});

describe("CSV export", () => {
  const report: LinkHealthReport = {
    platform: "1MiO",
    checked: 2,
    counts: countStatuses([]),
    results: [
      result("https://a.org", "Redirected", {
        httpStatus: 200,
        finalUrl: "https://a.org/new",
        durationMs: 812,
        reason: 'Moved, "permanently"',
      }),
      result('=HYPERLINK("x")', "Invalid", { reason: "Not a link" }),
    ],
    dateChecked: "2026-11-28T10:00:00Z",
  };

  test("a header and a row per link, quoting where needed", () => {
    const lines = linkHealthToCsv(report).split("\r\n");
    assert.equal(lines[0], LINK_HEALTH_CSV_HEADER.join(","));
    assert.equal(
      lines[1],
      'https://a.org,Redirected,Redirected,200,https://a.org/new,812,"Moved, ""permanently""",1MiO,2026-11-28T10:00:00Z',
    );
    assert.equal(lines.at(-1), "");
  });

  test("a cell that would run as a spreadsheet formula is made plain text", () => {
    const line = linkHealthToCsv(report).split("\r\n")[2]!;
    assert.ok(line.startsWith(`"'=HYPERLINK(""x"")",Invalid,`));
  });

  test("the file name carries the platform and the day", () => {
    const day = new Date("2026-11-28T10:00:00Z");
    assert.equal(
      linkHealthFileName("YouthHub (example)", day),
      "link-health-youthhub-example-2026-11-28.csv",
    );
    assert.equal(linkHealthFileName(null, day), "link-health-2026-11-28.csv");
  });
});
