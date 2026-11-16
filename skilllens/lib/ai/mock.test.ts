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
    expect(c.text).toBe("Amina sells tomatoes in Kano for ₦500 a basket. Sales drop on rainy days. What should Amina change?");
    const pcm = await mockProvider.generateChallenge(template, locale, "pcm", 0);
    expect(pcm.text).toContain("dey sell");
  });

  it("gives an exact example answer its educator scores", async () => {
    const fb = await score(examples[0].answer);
    expect(fb.scores).toEqual(examples[0].scores);
  });

  it("scores an empty answer 1 everywhere but still shows a strength and a suggestion", async () => {
    const fb = await score("");
    expect(Object.values(fb.scores)).toEqual([1, 1, 1]);
    expect(fb.strengths.length).toBeGreaterThan(0);
    expect(fb.suggestion.text).toBeTruthy();
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
