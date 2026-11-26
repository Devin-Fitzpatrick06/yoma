import { describe, expect, it } from "vitest";
import { POST as challenge } from "./challenge/route";
import { POST as feedback } from "./feedback/route";
import { POST as rating } from "./rating/route";
import { POST as transcribe } from "./transcribe/route";
import { POST as yoma } from "./yoma/route";

const json = (body: unknown) =>
  new Request("http://test", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });

describe("API routes", () => {
  it("returns a filled challenge for a course, and 400 for an unknown one", async () => {
    const res = await challenge(json({ course: "job-readiness", lang: "pcm" }));
    const body = await res.json();
    expect(body.templateId).toMatch(/^jr-/);
    expect(body.text).not.toMatch(/[{}]/);
    expect((await challenge(json({ course: "nope", lang: "en" }))).status).toBe(400);
  });

  const values = { name: "Amina", item: "tomatoes", city: "Kano", price: "₦500", unit: "basket" };

  it("returns strengths and a suggestion but never scores", async () => {
    const res = await feedback(json({ templateId: "fl-rainy-sales", values, lang: "en", answer: "Buy fewer tomatoes when it rains." }));
    const body = await res.json();
    expect(res.status).toBe(200);
    expect(body.strengths.length).toBeGreaterThan(0);
    expect(body.suggestion.text).toBeTruthy();
    expect(body).not.toHaveProperty("scores");
    expect(body.skills.map((k: { name: string }) => k.name)).toContain("Financial Literacy");
  });

  it("strips personal details and returns the cleaned answer for saving", async () => {
    const res = await feedback(json({ templateId: "fl-rainy-sales", values, lang: "en", answer: "Buy fewer tomatoes. My name is Musa Bello, 08031234567" }));
    const body = await res.json();
    expect(body.answer).toBe("Buy fewer tomatoes. My name is [name], [phone]");
    expect(body.redacted.sort()).toEqual(["name", "phone"]);
  });

  it("returns a flagged status, not feedback, for a harmful answer", async () => {
    const body = await (await feedback(json({ templateId: "fl-rainy-sales", values, lang: "en", answer: "I want to end my life" }))).json();
    expect(body).toEqual({ status: "flagged" });
  });

  it("rejects empty, oversized or malformed answers", async () => {
    const base = { templateId: "fl-rainy-sales", values, lang: "en" };
    expect((await feedback(json({ ...base, answer: "   " }))).status).toBe(400);
    expect((await feedback(json({ ...base, values: { ...values, name: "{x}" }, answer: "hi" }))).status).toBe(400);
    expect((await feedback(json({ ...base, values: { name: "Amina" }, answer: "hi" }))).status).toBe(400);
    expect((await feedback(json({ ...base, answer: "a".repeat(2001) }))).status).toBe(400);
    expect((await feedback(json({ ...base, templateId: "nope", answer: "hi" }))).status).toBe(400);
    expect((await feedback(new Request("http://test", { method: "POST", body: "not json" }))).status).toBe(400);
  });

  it("transcribes an image and rejects non-images", async () => {
    const form = (file: File) => {
      const f = new FormData();
      f.append("photo", file);
      return new Request("http://test", { method: "POST", body: f });
    };
    const ok = await transcribe(form(new File([new Uint8Array(10)], "a.jpg", { type: "image/jpeg" })));
    expect((await ok.json()).text).toContain("Mock transcription");
    expect((await transcribe(form(new File(["x"], "a.txt", { type: "text/plain" })))).status).toBe(400);
  });

  it("records a 1-5 rating and rejects anything else", async () => {
    expect((await rating(json({ templateId: "fl-rainy-sales", level: 1, rating: 4 }))).status).toBe(200);
    expect((await rating(json({ templateId: "fl-rainy-sales", level: 1, rating: 6 }))).status).toBe(400);
    expect((await rating(json({ templateId: "nope", level: 2, rating: 3 }))).status).toBe(400);
  });

  it("previews a Yoma submission with the server's own skills and a cleaned answer", async () => {
    const sample = {
      courseId: "financial-literacy",
      templateId: "fl-rainy-sales",
      lang: "en",
      scenario: "Amina sells tomatoes.",
      answer: "Buy fewer. Call 08031234567",
      skills: [{ id: "11111111-1111-1111-1111-111111111111", name: "Forged skill" }],
      savedAt: "2026-11-15T10:00:00.000Z",
      label: "Practice sample, not a certification",
    };
    const body = await (await yoma(json({ sample }))).json();
    expect(body).toMatchObject({ status: "ok", mode: "mock" });
    expect(body.skills).not.toContain("Forged skill");
    expect(body.skills).toContain("Budgeting");
    expect((await yoma(json({ sample: { ...sample, courseId: "job-readiness" } }))).status).toBe(400);
    expect((await yoma(json({ sample: { ...sample, answer: "" } }))).status).toBe(400);
  });
});
