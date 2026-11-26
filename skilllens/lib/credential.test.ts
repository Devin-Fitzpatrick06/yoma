import { describe, expect, it } from "vitest";
import { passed } from "./credential";

const row = (id: string, level: number) => ({ id, name: id, level, now: "", next: null, before: null });

describe("credential pass rule", () => {
  it("passes only when every criterion is Good or Strong", () => {
    expect(passed([row("a", 3), row("b", 4), row("c", 3), row("d", 4)])).toBe(true);
    expect(passed([row("a", 4), row("b", 4), row("c", 2), row("d", 4)])).toBe(false);
    expect(passed([])).toBe(false);
  });
});
