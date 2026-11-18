import { describe, expect, it } from "vitest";
import { groundStrengths } from "./grounding";

const fb = (...texts: string[]) => ({
  scores: { cause: 1, action: 1, numbers: 1 },
  strengths: texts.map((text, i) => ({ criterionId: ["cause", "action"][i] ?? "cause", text })),
  suggestion: { criterionId: "numbers", text: "Answer the question about costs." },
});

describe("groundStrengths", () => {
  it("replaces praise for ideas the answer never mentions (scenario E2)", () => {
    const out = groundStrengths(fb("You mentioned the ingredients cost more."), "I like football and jollof rice very much on Sundays.", "en");
    expect(out.dropped).toBe(1);
    expect(out.feedback.strengths).toEqual([{ criterionId: "cause", text: "You tried the challenge and wrote in your own words." }]);
  });

  it("keeps strengths that point at the learner's own words, including plurals and Pidgin", () => {
    const answer = "She should buy fewer baskets when rain is forecast. I go call the manager.";
    const out = groundStrengths(fb("Buying fewer basket on rainy days cuts waste.", "You called your manager early."), answer, "en");
    expect(out.dropped).toBe(0);
  });

  it("drops only the invented one when another strength is grounded", () => {
    const out = groundStrengths(fb("You saw that rain stops customers.", "You worked out the profit."), "Rain keeps customers at home so sales drop.", "pcm");
    expect(out.dropped).toBe(1);
    expect(out.feedback.strengths.map((s) => s.text)).toEqual(["You saw that rain stops customers."]);
  });
});
