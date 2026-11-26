import { describe, expect, it } from "vitest";
import { isConfused } from "./confusion";

describe("isConfused", () => {
  it("spots learners saying the question is confusing, in English and Pidgin", () => {
    for (const a of ["I'm still confused, this question is bad", "I don't understand what to do here", "what does this mean?", "I no understand wetin dem mean", "this question is not clear to me"])
      expect(isConfused(a), a).toBe(true);
  });

  it("scores a real attempt, even an unsure one", () => {
    for (const a of ["I'm not sure but profit is ₦50 minus ₦40 = ₦10", "Amina should save some money every day because rain is common", "The loan terms make no sense to me because ₦30,000 back on ₦20,000 is 50% interest"])
      expect(isConfused(a), a).toBe(false);
  });
});
