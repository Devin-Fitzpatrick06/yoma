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
    const bad = { ...mockProvider, scoreAnswer: async () => ({ scores: { cause: 1, action: 1, numbers: 1 }, strengths: [{ criterionId: "cause", text: "You are naked" }], suggestion: { criterionId: "numbers", text: "x" } }) };
    const out = await scoreSafely(request("Buy fewer tomatoes when raining"), { provider: bad, safety: stubSafety });
    expect(out.status === "ok" && out.fallback).toBe(true);
    expect(JSON.stringify(out)).not.toContain("naked");
    expect(listFlags()[0]).toMatchObject({ stage: "feedback", excerpt: "" });
  });

  it("drops model praise for ideas the answer never contains", async () => {
    const inventive = { ...mockProvider, scoreAnswer: async () => ({ scores: { cause: 2, action: 1, numbers: 1 }, strengths: [{ criterionId: "cause", text: "You mentioned the ingredients cost more." }], suggestion: { criterionId: "numbers", text: "Answer the question." } }) };
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
