// Shared helpers for the hand-rated validation sample (zeki-vs-current-review.md, "How to measure better").

// Seeded random numbers (mulberry32), so a sample can be drawn again exactly from its seed.
export function seeded(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function shuffle(items, random) {
  const out = [...items];
  for (let i = out.length - 1; i > 0; i--) {
    const j = Math.floor(random() * (i + 1));
    [out[i], out[j]] = [out[j], out[i]];
  }
  return out;
}

// Minimal CSV: quoted fields, commas and newlines inside quotes, a header row.
export function toCsv(rows, columns) {
  const cell = (v) => {
    const s = v === null || v === undefined ? "" : String(v);
    return /[",\n]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s;
  };
  return [columns.join(","), ...rows.map((r) => columns.map((c) => cell(r[c])).join(","))].join("\n") + "\n";
}

export function fromCsv(text) {
  const rows = [];
  let row = [], field = "", quoted = false;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (quoted) {
      if (ch === '"' && text[i + 1] === '"') { field += '"'; i++; }
      else if (ch === '"') quoted = false;
      else field += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ",") { row.push(field); field = ""; }
    else if (ch === "\n" || ch === "\r") {
      if (ch === "\r" && text[i + 1] === "\n") i++;
      row.push(field); field = "";
      if (row.some((f) => f !== "")) rows.push(row);
      row = [];
    } else field += ch;
  }
  if (field !== "" || row.length) { row.push(field); if (row.some((f) => f !== "")) rows.push(row); }
  const [header, ...body] = rows;
  return body.map((r) => Object.fromEntries(header.map((h, i) => [h.trim(), (r[i] ?? "").trim()])));
}

// Cohen's kappa for two raters over the same items. Weighted (linear) when an ordered scale is given.
export function kappa(a, b, scale = null) {
  const n = a.length;
  if (!n) return null;
  const cats = scale ?? [...new Set([...a, ...b])];
  const k = cats.length;
  if (k < 2) return null;
  const idx = new Map(cats.map((c, i) => [c, i]));
  const w = (i, j) => (scale ? 1 - Math.abs(i - j) / (k - 1) : i === j ? 1 : 0);
  const pa = new Array(k).fill(0), pb = new Array(k).fill(0);
  let observed = 0;
  for (let t = 0; t < n; t++) {
    const i = idx.get(a[t]), j = idx.get(b[t]);
    pa[i]++; pb[j]++;
    observed += w(i, j);
  }
  observed /= n;
  let expected = 0;
  for (let i = 0; i < k; i++) for (let j = 0; j < k; j++) expected += w(i, j) * (pa[i] / n) * (pb[j] / n);
  return expected === 1 ? null : (observed - expected) / (1 - expected);
}

// Percentile bootstrap 95% interval of a statistic over paired items.
export function bootstrap(items, statistic, random, rounds = 1000) {
  const values = [];
  for (let r = 0; r < rounds; r++) {
    const resample = items.map(() => items[Math.floor(random() * items.length)]);
    const v = statistic(resample);
    if (v !== null && Number.isFinite(v)) values.push(v);
  }
  if (values.length < rounds / 2) return null;
  values.sort((x, y) => x - y);
  return [values[Math.floor(0.025 * values.length)], values[Math.ceil(0.975 * values.length) - 1]];
}
