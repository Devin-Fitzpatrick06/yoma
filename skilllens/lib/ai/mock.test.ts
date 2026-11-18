import { describe, expect, it } from "vitest";
import { examplesFor, getRubric, getTemplate, locale } from "../data";
import { Feedback } from "../schema";
import { mockProvider } from "./mock";
import { getProvider } from "./provider";

const template = getTemplate("fl-rainy-sales");
const rubric = getRubric(template.rubricId);
const examples = examplesFor(template.id);

async function score(answer: string, lang: "en" | "pcm" = "en") {
  const challenge = await mockProvider.generateChallenge(template, locale, lang, 0);
  return mockProvider.scoreAnswer({ challenge, rubric, examples, answer, lang });
}

describe("mock provider", () => {
  it("fills the template with local details", async () => {
    const c = await mockProvider.generateChallenge(template, locale, "en", 0);
    expect(c.text).toBe("Amina sells tomatoes in Kano for ₦500 a basket. On rainy days sales drop and some tomatoes spoil. How can Amina use sales records and an emergency fund to cope?");
    const pcm = await mockProvider.generateChallenge(template, locale, "pcm", 0);
    expect(pcm.text).toContain("dey sell");
  });

  it("scores from what the answer contains, and never asks for numbers when there are sums", async () => {
    const fb = await score("Because rain keeps customers home, Amina loses money. She sells 4 fewer baskets, so 4 x ₦500 = ₦2,000 lost. She should buy 6 baskets instead of 10 on rainy days.");
    expect(fb.scores.numbers).toBe(4);
    expect(fb.suggestion.criterionId).not.toBe("numbers");
    expect(fb.strengths.some((s) => s.text.includes("“"))).toBe(true); // quotes the learner's own words
  });

  it("scores an empty answer 1 everywhere but still shows a strength and a suggestion", async () => {
    const fb = await score("");
    expect(Object.values(fb.scores)).toEqual([1, 1, 1, 1]);
    expect(fb.strengths.length).toBeGreaterThan(0);
    expect(fb.suggestion.text).toBeTruthy();
  });

  it("on Level 2, moves to a new tip once the last one was acted on", async () => {
    const first = await score("Because rain keeps customers home, Amina should keep a record of sales and save in an emergency fund.");
    const second = await mockProvider.scoreAnswer({
      challenge: await mockProvider.generateChallenge(template, locale, "en", 0),
      rubric, examples, lang: "en",
      answer: "Because rain keeps customers home, Amina should keep a record of sales and save in an emergency fund. If she sells 4 fewer baskets at ₦500 she loses ₦2,000.",
      previous: { answer: "x", levels: first.scores, suggestionCriterionId: first.suggestion.criterionId },
    });
    expect(first.suggestion.criterionId).toBe("numbers");
    expect(second.scores.numbers).toBeGreaterThan(first.scores.numbers);
    expect(second.suggestion.criterionId).not.toBe("numbers");
  });

  it("returns schema-valid feedback in the learner's language", async () => {
    const fb = await score("Amina can buy fewer tomatoes when rain is forecast.", "pcm");
    expect(() => Feedback.parse(fb)).not.toThrow();
    const texts = [...rubric.criteria.map((c) => c.strength.pcm), ...rubric.criteria.map((c) => c.improve.pcm)];
    expect(texts).toContain(fb.suggestion.text);
  });

  it("rejects unknown providers", () => {
    expect(getProvider("mock")).toBe(mockProvider);
    expect(() => getProvider("nope")).toThrow(/Unknown AI_PROVIDER/);
  });
});
