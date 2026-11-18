// AI-educator agreement on a 1-4 rubric scale.

export interface Agreement {
  n: number;
  exact: number; // share of answers where AI score == educator score
  adjacent: number; // share within one level
  qwk: number; // quadratic weighted kappa: 1 = perfect, 0 = chance, < 0 = worse than chance
}

export function agreement(educator: number[], ai: number[], levels = 4): Agreement {
  if (educator.length !== ai.length) throw new Error("Score lists differ in length");
  const n = educator.length;
  if (n === 0) return { n, exact: NaN, adjacent: NaN, qwk: NaN };

  const observed = Array.from({ length: levels }, () => new Array<number>(levels).fill(0));
  const histE = new Array<number>(levels).fill(0);
  const histA = new Array<number>(levels).fill(0);
  let exact = 0;
  let adjacent = 0;
  for (let i = 0; i < n; i++) {
    const e = educator[i] - 1;
    const a = ai[i] - 1;
    if (!(e >= 0 && e < levels && a >= 0 && a < levels)) throw new Error(`Score out of range at ${i}`);
    observed[e][a]++;
    histE[e]++;
    histA[a]++;
    if (e === a) exact++;
    if (Math.abs(e - a) <= 1) adjacent++;
  }

  let num = 0;
  let den = 0;
  for (let i = 0; i < levels; i++)
    for (let j = 0; j < levels; j++) {
      const w = (i - j) ** 2 / (levels - 1) ** 2;
      num += w * observed[i][j];
      den += (w * histE[i] * histA[j]) / n;
    }
  // Both raters used a single identical level: no disagreement is possible, so agreement is perfect.
  const qwk = den === 0 ? 1 : 1 - num / den;
  return { n, exact: exact / n, adjacent: adjacent / n, qwk };
}
