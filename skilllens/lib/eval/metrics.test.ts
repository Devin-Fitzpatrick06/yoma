import { describe, expect, it } from "vitest";
import { agreement } from "./metrics";

describe("agreement", () => {
  it("is perfect for identical scores", () => {
    expect(agreement([1, 2, 3, 4], [1, 2, 3, 4])).toEqual({ n: 4, exact: 1, adjacent: 1, qwk: 1 });
  });

  it("matches a hand-worked quadratic weighted kappa", () => {
    // Educator [1,2,3,4], AI [2,2,3,3]: weighted disagreement 2/9, chance (1/2)(5+1+1+5)/9 = 6/9
    // -> kappa 1 - (2/9)/(6/9) = 2/3
    const a = agreement([1, 2, 3, 4], [2, 2, 3, 3]);
    expect(a.exact).toBe(0.5);
    expect(a.adjacent).toBe(1);
    expect(a.qwk).toBeCloseTo(2 / 3, 10);
  });

  it("goes negative when the AI reverses the scale", () => {
    expect(agreement([1, 2, 3, 4], [4, 3, 2, 1]).qwk).toBeCloseTo(-1, 10);
  });

  it("rejects out-of-range or mismatched scores", () => {
    expect(() => agreement([1, 5], [1, 1])).toThrow(/out of range/);
    expect(() => agreement([1], [1, 2])).toThrow(/length/);
  });
});
