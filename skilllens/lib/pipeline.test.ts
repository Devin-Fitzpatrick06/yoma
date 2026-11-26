import { beforeEach, describe, expect, it, vi } from "vitest";
import { mockProvider } from "./ai/mock";
import { examplesFor, getRubric, getTemplate, locale } from "./data";
import { challengeSafely, scoreSafely, transcribeSafely, type Deps } from "./pipeline";
import { stubSafety, type ContentSafety } from "./safety/contentSafety";
import { listFlags, resetStore } from "./store";

const template = getTemplate("fl-rainy-sales");
const rubric = getRubric(template.rubricId);
const challenge = { templateId: template.id, courseId: template.courseId, rubricId: rubric.id, lang: "en" as const, values: {}, text: "Scenario" };
const request = (answer: string) => ({ challenge, rubric, examples: examplesFor(template.id), answer, lang: "en" as const });

beforeEach(resetStore);

describe("scoreSafely", () => {
  it("strips personal details before the model sees the answer", async () => {
    const provider = { ...mockProvider, scoreAnswer: vi.fn(mockProvider.scoreAnswer) };
    const out = await scoreSafely(request("Buy fewer tomatoes when it rains. Call me on 08031234567"), { provider, safety: stubSafety });
    expect(provider.scoreAnswer.mock.calls[0][0].answer).toBe("Buy fewer tomatoes when it rains. Call me on [phone]");
    expect(out).toMatchObject({ status: "ok", redacted: ["phone"], fallback: false });
  });

  it("does not call the model for a flagged answer, and records a flag without the raw text", async () => {
    const provider = { ...mockProvider, scoreAnswer: vi.fn(mockProvider.scoreAnswer) };
    const out = await scoreSafely(request("I want to kill myself. My name is Musa Bello"), { provider, safety: stubSafety });
    expect(out.status).toBe("flagged");
    expect(provider.scoreAnswer).not.toHaveBeenCalled();
    expect(listFlags()[0]).toMatchObject({ stage: "answer", templateId: template.id });
    expect(listFlags()[0].excerpt).not.toContain("Musa");
  });

  it("replaces flagged model feedback with educator-written rubric text", async () => {
    const bad = { ...mockProvider, scoreAnswer: async () => ({ scores: { cause: 1, tools: 1, action: 1, numbers: 1 }, strengths: [{ criterionId: "cause", text: "x" }], suggestion: { criterionId: "numbers", text: "You are naked" } }) };
    const out = await scoreSafely(request("Buy fewer tomatoes when raining"), { provider: bad, safety: stubSafety });
    expect(out.status === "ok" && out.fallback).toBe(true);
    expect(JSON.stringify(out)).not.toContain("naked");
    expect(listFlags()[0]).toMatchObject({ stage: "feedback", excerpt: "" });
  });

  it("explains the question instead of scoring when the learner says they are confused", async () => {
    let called = false;
    const spy = { ...mockProvider, scoreAnswer: async (i: Parameters<typeof mockProvider.scoreAnswer>[0]) => { called = true; return mockProvider.scoreAnswer(i); } };
    const out = await scoreSafely(request("I'm still confused, this question is bad"), { provider: spy, safety: stubSafety });
    expect(out.status).toBe("confused");
    expect(called).toBe(false);
  });

  it("never says 'Did well' about a criterion the answer barely touches", async () => {
    const model = { scores: { cause: 3, tools: 3, action: 3, numbers: 3 }, strengths: [{ criterionId: "cause", text: "You explained what is causing the money problem." }], suggestion: { criterionId: "numbers", text: "Add numbers." } };
    const out = await scoreSafely(request("Amina should think about it and do better next time"), { provider: { ...mockProvider, scoreAnswer: async () => model }, safety: stubSafety });
    expect(out.status === "ok" && out.feedback.strengths.map((s) => s.text)).toEqual(["You tried the challenge and wrote in your own words."]);
  });

  it("drops model praise for ideas the answer never contains", async () => {
    const inventive = { ...mockProvider, scoreAnswer: async () => ({ scores: { cause: 2, tools: 1, action: 1, numbers: 1 }, strengths: [{ criterionId: "cause", text: "You mentioned the ingredients cost more." }], suggestion: { criterionId: "numbers", text: "Answer the question." } }) };
    const out = await scoreSafely(request("I like football and jollof rice on Sundays"), { provider: inventive, safety: stubSafety });
    expect(out.status === "ok" && out.ungrounded).toBe(1);
    expect(out.status === "ok" && out.feedback.strengths[0].text).toBe("You tried the challenge and wrote in your own words.");
  });

  it("fails closed when screening is down", async () => {
    const down: ContentSafety = { checkText: async () => { throw new Error("503"); }, checkImage: async () => { throw new Error("503"); } };
    await expect(scoreSafely(request("Buy fewer tomatoes when it rains"), { provider: mockProvider, safety: down })).rejects.toThrow("503");
  });

  it("asks for more on answers under four words, without calling the model or the screen", async () => {
    const provider = { ...mockProvider, scoreAnswer: vi.fn(mockProvider.scoreAnswer) };
    const safety = { ...stubSafety, checkText: vi.fn(stubSafety.checkText) };
    for (const answer of ["ok", "I don't know", "   sell   more  "]) expect(await scoreSafely(request(answer), { provider, safety })).toEqual({ status: "too_short" });
    expect(provider.scoreAnswer).not.toHaveBeenCalled();
    expect(safety.checkText).not.toHaveBeenCalled();
  });
});

describe("challengeSafely and transcribeSafely", () => {
  it("falls back to default local details when a generated challenge is flagged", async () => {
    const deps: Deps = {
      provider: { ...mockProvider, generateChallenge: async (t, l, lang) => ({ ...(await mockProvider.generateChallenge(t, l, lang, 0)), text: "naked" }) },
      safety: stubSafety,
    };
    expect((await challengeSafely(template, locale, "en", 0, deps)).text).not.toContain("naked");
    expect(listFlags()[0].stage).toBe("challenge");
  });

  it("screens the photo, then strips and screens the transcription", async () => {
    const provider = { ...mockProvider, transcribePhoto: async () => "Sell more. my name na Chidi" };
    expect(await transcribeSafely(new Uint8Array([1]), "image/jpeg", { provider, safety: stubSafety })).toEqual({
      status: "ok",
      text: "Sell more. my name na [name]",
      redacted: ["name"],
    });
    const blocked: ContentSafety = { ...stubSafety, checkImage: async () => ({ flagged: true, categories: [{ category: "Sexual", severity: 6 }] }) };
    expect((await transcribeSafely(new Uint8Array([1]), "image/jpeg", { provider, safety: blocked })).status).toBe("flagged");
  });
});

describe("checkAgainstAnswer", () => {
  it("never tells a learner with sums to add numbers, and moves the tip to the weakest criterion", async () => {
    const { checkAgainstAnswer } = await import("./pipeline");
    const { getRubric, getTemplate } = await import("./data");
    const template = getTemplate("fl-rainy-sales");
    const rubric = getRubric(template.rubricId);
    const challenge = await mockProvider.generateChallenge(template, (await import("./data")).locale, "en", 0);
    const answer = "She loses 4 x ₦500 = ₦2,000 each rainy day.";
    const model = { scores: { cause: 2, tools: 1, action: 2, numbers: 1 }, strengths: [{ criterionId: "cause", text: "x" }], suggestion: { criterionId: "numbers", text: "Add numbers." } };
    const out = checkAgainstAnswer(model, { challenge, rubric, examples: [], answer, lang: "en" }, answer);
    expect(out.scores.numbers).toBe(3);
    expect(out.suggestion.criterionId).toBe("tools");
  });

  it("keeps a model's level within one step of the evidence, so no plan means no 'Good' plan", async () => {
    const { checkAgainstAnswer } = await import("./pipeline");
    const data = await import("./data");
    const template = data.getTemplate("fl-ingredient-costs");
    const rubric = data.getRubric(template.rubricId);
    const challenge = await mockProvider.generateChallenge(template, data.locale, "en", 0);
    const answer = "Flour and oil cost more, so each one now costs ₦40 to make instead of ₦30, and profit fell.";
    const model = { scores: { cause: 2, tools: 3, action: 3, numbers: 4 }, strengths: [{ criterionId: "numbers", text: "x" }], suggestion: { criterionId: "cause", text: "Say the cause." } };
    const out = checkAgainstAnswer(model, { challenge, rubric, examples: [], answer, lang: "en" }, answer);
    expect(out.scores.action).toBe(2); // no step at all: at most "Getting there"
    expect(out.scores.cause).toBe(3); // the cause is explained with "so"
    expect(out.suggestion.criterionId).toBe("action");
  });
});

describe("scoreSafely with a Level 1 attempt", () => {
  it("strips personal details from the previous answer and screens it before the model sees it", async () => {
    const { scoreSafely } = await import("./pipeline");
    const data = await import("./data");
    const template = data.getTemplate("fl-rainy-sales");
    const rubric = data.getRubric(template.rubricId);
    const challenge = await mockProvider.generateChallenge(template, data.locale, "en", 0);
    let seen = "";
    const spy = { ...mockProvider, scoreAnswer: async (i: Parameters<typeof mockProvider.scoreAnswer>[0]) => { seen = i.previous?.answer ?? ""; return mockProvider.scoreAnswer(i); } };
    const safe = { checkText: async (t: string) => ({ flagged: /end my life/i.test(t), categories: [] }), checkImage: async () => ({ flagged: false, categories: [] }) };
    const req = { challenge, rubric, examples: [], answer: "Amina should buy fewer tomatoes when rain is forecast.", lang: "en" as const };
    const levels = { cause: 1, tools: 1, numbers: 1, action: 1 };
    await scoreSafely({ ...req, previous: { answer: "Call me on 08012345678 about the tomatoes.", levels, suggestionCriterionId: "cause" } }, { provider: spy, safety: safe });
    expect(seen).not.toContain("08012345678");
    const out = await scoreSafely({ ...req, previous: { answer: "I want to end my life.", levels, suggestionCriterionId: "cause" } }, { provider: spy, safety: safe });
    expect(out.status).toBe("flagged");
  });
});
