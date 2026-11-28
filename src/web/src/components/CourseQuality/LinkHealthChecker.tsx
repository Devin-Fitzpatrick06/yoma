import { useCallback, useId, useMemo, useRef, useState } from "react";
import type {
  LinkHealthReport,
  LinkHealthRequest,
} from "~/api/models/courseQuality";
import { BTN_PRIMARY, BTN_SECONDARY } from "~/components/Common/buttonStyles";
import { ApiErrors } from "~/components/Status/ApiErrors";
import {
  columnNames,
  columnUrls,
  detectDelimiter,
  findUrlColumn,
  LINK_HEALTH_BATCH_SIZE,
  mergeReports,
  parseCsv,
  parseUrlList,
  toBatches,
  uniqueUrls,
} from "./LinkHealth.helpers";
import LinkHealthResults from "./LinkHealthResults";

// ─────────────────────────────────────────────────────────────────────────────
// Link health checker (Ziqi iteration 5): paste links or upload a .txt/.csv (read in the browser),
// pick the link column, label the platform, then check them in batches of 200 with progress.
// `check` is the API call on /admin/course-quality/links and a fake on the fixture preview.
// ─────────────────────────────────────────────────────────────────────────────

const MAX_FILE_BYTES = 5 * 1024 * 1024;

interface UploadedFile {
  name: string;
  /** CSV rows; a .txt file is one column. */
  rows: string[][];
  columns: string[];
  column: number;
}

const readFile = async (file: File): Promise<UploadedFile> => {
  const text = await file.text();
  const isCsv =
    /\.csv$/i.test(file.name) ||
    file.type === "text/csv" ||
    (!/\.txt$/i.test(file.name) && /[,;\t]/.test(text.split("\n", 1)[0] ?? ""));
  const rows = isCsv
    ? parseCsv(text, detectDelimiter(text))
    : parseUrlList(text).map((url) => [url]);
  const column = isCsv ? findUrlColumn(rows) : 0;
  return {
    name: file.name,
    rows,
    columns: isCsv ? columnNames(rows) : ["Links"],
    column,
  };
};

const LinkHealthChecker: React.FC<{
  check: (request: LinkHealthRequest) => Promise<LinkHealthReport>;
  initialText?: string;
  initialPlatform?: string;
  initialReport?: LinkHealthReport | null;
}> = ({
  check,
  initialText = "",
  initialPlatform = "",
  initialReport = null,
}) => {
  const id = useId();
  const [text, setText] = useState(initialText);
  const [platform, setPlatform] = useState(initialPlatform);
  const [upload, setUpload] = useState<UploadedFile | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [report, setReport] = useState<LinkHealthReport | null>(initialReport);
  const [progress, setProgress] = useState<{
    done: number;
    total: number;
  } | null>(null);
  const [runError, setRunError] = useState<unknown>(null);
  const [stopped, setStopped] = useState(false);
  const stopRef = useRef(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const urls = useMemo(
    () =>
      uniqueUrls([
        ...parseUrlList(text),
        ...(upload ? columnUrls(upload.rows, upload.column) : []),
      ]),
    [text, upload],
  );
  const batches = Math.ceil(urls.length / LINK_HEALTH_BATCH_SIZE);
  const running = progress != null;

  const onFile = useCallback(async (file: File | undefined) => {
    setFileError(null);
    if (!file) return;
    if (file.size > MAX_FILE_BYTES) {
      setFileError("That file is over 5 MB. Split it and upload the parts.");
      return;
    }
    try {
      const parsed = await readFile(file);
      if (parsed.rows.length === 0) {
        setFileError("That file has no rows.");
        return;
      }
      setUpload(parsed);
    } catch {
      setFileError("That file could not be read. Upload a .txt or .csv file.");
    }
  }, []);

  const run = useCallback(async () => {
    if (urls.length === 0 || running) return;
    const label = platform.trim() || null;
    const parts = toBatches(urls);
    const reports: LinkHealthReport[] = [];
    stopRef.current = false;
    setStopped(false);
    setRunError(null);
    setReport(null);
    setProgress({ done: 0, total: urls.length });
    try {
      for (const part of parts) {
        if (stopRef.current) break;
        reports.push(await check({ urls: part, platform: label }));
        setProgress({
          done: reports.reduce((sum, r) => sum + r.results.length, 0),
          total: urls.length,
        });
        setReport(mergeReports(reports, label));
      }
      setStopped(stopRef.current);
    } catch (error) {
      setRunError(error);
    } finally {
      setProgress(null);
    }
  }, [urls, running, platform, check]);

  const percent =
    progress && progress.total > 0
      ? Math.round((progress.done / progress.total) * 100)
      : 0;

  return (
    <div className="flex flex-col gap-4">
      <section
        aria-labelledby={`${id}-heading`}
        className="flex flex-col gap-4 rounded-lg bg-white p-4 shadow-sm md:p-5"
      >
        <h2
          id={`${id}-heading`}
          className="font-nunito text-lg font-black text-black"
        >
          Links to check
        </h2>

        <div className="grid gap-4 lg:grid-cols-2">
          {/* PASTE */}
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-semibold">Paste links, one per line</span>
            <textarea
              className="textarea textarea-bordered min-h-40 w-full font-mono text-xs"
              value={text}
              onChange={(e) => setText(e.target.value)}
              placeholder={
                "https://example.org/course-1\nhttps://example.org/course-2"
              }
              spellCheck={false}
              disabled={running}
              aria-describedby={`${id}-count`}
            />
          </label>

          <div className="flex flex-col gap-4">
            {/* UPLOAD */}
            <div className="flex flex-col gap-1 text-sm">
              <label htmlFor={`${id}-file`} className="font-semibold">
                Or upload a .txt or .csv file
              </label>
              <input
                id={`${id}-file`}
                ref={fileInput}
                type="file"
                accept=".txt,.csv,text/plain,text/csv"
                className="file-input file-input-bordered file-input-sm w-full max-w-full"
                disabled={running}
                aria-describedby={`${id}-file-help`}
                onChange={(e) => void onFile(e.target.files?.[0])}
              />
              <span id={`${id}-file-help`} className="text-gray-dark text-xs">
                Read in your browser; nothing is uploaded until you check.
              </span>
              {fileError && (
                <span role="alert" className="text-sm text-red-500">
                  {fileError}
                </span>
              )}
              {upload && (
                <div className="bg-gray-light flex flex-col gap-2 rounded-lg px-3 py-2">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="min-w-0 text-xs break-all">
                      <span className="font-semibold">{upload.name}</span>:{" "}
                      {columnUrls(upload.rows, upload.column).length} links
                    </span>
                    <button
                      type="button"
                      className="text-theme text-xs font-semibold hover:underline"
                      disabled={running}
                      onClick={() => {
                        setUpload(null);
                        if (fileInput.current) fileInput.current.value = "";
                      }}
                    >
                      Remove file
                    </button>
                  </div>
                  {upload.columns.length > 1 && (
                    <label className="flex flex-col gap-1 text-xs">
                      <span className="font-semibold">
                        Column with the links
                      </span>
                      <select
                        className="select select-sm w-full"
                        value={upload.column}
                        disabled={running}
                        onChange={(e) =>
                          setUpload({
                            ...upload,
                            column: Number(e.target.value),
                          })
                        }
                      >
                        {upload.column < 0 && (
                          <option value={-1}>Choose a column</option>
                        )}
                        {upload.columns.map((name, index) => (
                          <option key={`${name}-${index}`} value={index}>
                            {name}
                          </option>
                        ))}
                      </select>
                    </label>
                  )}
                </div>
              )}
            </div>

            {/* PLATFORM */}
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-semibold">Platform (optional)</span>
              <input
                type="text"
                className="input input-bordered input-sm w-full"
                value={platform}
                maxLength={100}
                placeholder="e.g. YouthHub or 1MiO"
                disabled={running}
                onChange={(e) => setPlatform(e.target.value)}
              />
              <span className="text-gray-dark text-xs">
                A label carried into the report and the CSV.
              </span>
            </label>
          </div>
        </div>

        {/* RUN */}
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <p id={`${id}-count`} className="text-gray-dark text-sm">
            {urls.length === 0
              ? "No links yet."
              : `${urls.length} unique ${urls.length === 1 ? "link" : "links"}${
                  batches > 1
                    ? `, checked in ${batches} batches of up to ${LINK_HEALTH_BATCH_SIZE}`
                    : ""
                }.`}
          </p>
          <div className="flex flex-col gap-2 sm:flex-row">
            {running && (
              <button
                type="button"
                className={BTN_SECONDARY}
                disabled={stopped}
                onClick={() => {
                  stopRef.current = true;
                  setStopped(true);
                }}
              >
                Stop after this batch
              </button>
            )}
            <button
              type="button"
              className={BTN_PRIMARY}
              disabled={urls.length === 0 || running}
              onClick={() => void run()}
            >
              {running ? "Checking..." : "Check links"}
            </button>
          </div>
        </div>

        {/* PROGRESS */}
        <div aria-live="polite" className="flex flex-col gap-1">
          {progress && (
            <>
              <p className="text-sm text-black">
                Checked {progress.done} of {progress.total} links
                {stopped ? "; stopping after this batch" : ""}. Each site is
                paced, so a batch can take a few minutes.
              </p>
              <div
                role="progressbar"
                aria-label="Links checked"
                aria-valuemin={0}
                aria-valuemax={100}
                aria-valuenow={percent}
                className="bg-gray h-1.5 w-full overflow-hidden rounded-full"
              >
                <div
                  className="bg-theme h-full rounded-full transition-[width] duration-300 motion-reduce:transition-none"
                  style={{ width: `${percent}%` }}
                />
              </div>
            </>
          )}
          {!progress && stopped && report && (
            <p className="text-sm text-black">
              Stopped: {report.checked} of {urls.length} links were checked.
            </p>
          )}
        </div>

        {runError != null && (
          <div
            role="alert"
            className="bg-orange-light flex flex-col gap-1 rounded-lg px-3 py-2 text-sm text-black"
          >
            <span className="font-semibold">
              The check stopped
              {report ? ` after ${report.checked} links` : ""}.
            </span>
            <ApiErrors error={runError} />
          </div>
        )}
      </section>

      {report && <LinkHealthResults report={report} />}
    </div>
  );
};

export default LinkHealthChecker;
