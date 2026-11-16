import { afterEach, describe, expect, it, vi } from "vitest";
import { rubrics } from "../data";
import type { PracticeSample } from "../schema";
import { submitToYoma, yomaConfigFromEnv } from "./client";
import { samplePdf, verifyCsv, verifyForm } from "./evidence";
import { mappedCriteria, skillsFor } from "./skills";

const sample: PracticeSample = {
  courseId: "financial-literacy",
  templateId: "fl-rainy-sales",
  lang: "en",
  scenario: "Amina sells tomatoes in Kano for ₦500 a basket. Sales drop on rainy days. What should Amina change?",
  answer: "Buy fewer baskets when rain is forecast (₦1,500 saved) and sell to regulars on WhatsApp.",
  skills: skillsFor("fl-rubric"),
  savedAt: "2026-11-15T10:00:00.000Z",
  label: "Practice sample, not a certification",
};

afterEach(() => vi.unstubAllGlobals());

describe("Yoma skills", () => {
  it("maps every rubric criterion to cached Yoma skills", () => {
    for (const r of rubrics) {
      expect(mappedCriteria(r.id).sort()).toEqual(r.criteria.map((c) => c.id).sort());
      expect(skillsFor(r.id).length).toBeGreaterThan(0);
    }
    expect(skillsFor("fl-rubric").map((s) => s.name)).toContain("Financial Literacy");
  });
});

describe("evidence", () => {
  it("builds a valid one-page PDF with a correct cross-reference table", () => {
    const pdf = new TextDecoder().decode(samplePdf(sample, "Financial Literacy"));
    expect(pdf.startsWith("%PDF-1.4")).toBe(true);
    expect(pdf).toContain("(Practice sample, not a certification) Tj");
    expect(pdf).toContain("NGN 500");
    const xref = Number(pdf.match(/startxref\n(\d+)/)![1]);
    expect(pdf.slice(xref, xref + 4)).toBe("xref");
    const offsets = [...pdf.matchAll(/^(\d{10}) 00000 n $/gm)].map((m) => Number(m[1]));
    offsets.forEach((o, i) => expect(pdf.slice(o).startsWith(`${i + 1} 0 obj`)).toBe(true));
  });

  it("uses Yoma's verify field names", () => {
    const form = verifyForm(sample, "Financial Literacy");
    expect([...form.keys()]).toEqual(["Certificate", "DateStart", "DateEnd", "Feedback"]);
    expect((form.get("Certificate") as File).name).toMatch(/\.pdf$/);
  });

  it("writes Yoma's CSV import columns and refuses rows Yoma cannot match", () => {
    const csv = verifyCsv([{ learner: { email: "a@example.org", firstName: "Ada, Jr" }, sample, opportunityExternalId: "P2E-FL-01" }]);
    expect(csv).toBe("Email,PhoneNumber,FirstName,Surname,Gender,Country,DateCompleted,OpportunityExternalId\na@example.org,,\"Ada, Jr\",,,,2026-11-15,P2E-FL-01\n");
    expect(() => verifyCsv([{ learner: {}, sample, opportunityExternalId: "x" }])).toThrow(/email or a phone/);
  });
});

describe("Yoma client", () => {
  it("previews the request in mock mode without sending anything", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const res = await submitToYoma(yomaConfigFromEnv({}), sample, "Financial Literacy");
    expect(res).toMatchObject({ mode: "mock", fields: ["Certificate", "DateStart", "DateEnd", "Feedback"] });
    expect(res.endpoint).toMatch(/^https:\/\/v3api\.stage\.yoma\.world\/api\/v3\/myOpportunity\/action\/.+\/verify$/);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("sends a PUT with the learner's token in live mode", async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    const config = yomaConfigFromEnv({ YOMA_MODE: "live", YOMA_OPPORTUNITY_IDS: "financial-literacy:11111111-1111-1111-1111-111111111111" });
    await expect(submitToYoma(config, sample, "FL")).rejects.toThrow(/access token/);
    expect(await submitToYoma(config, sample, "FL", "tok")).toMatchObject({ mode: "live", status: 200 });
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toContain("/myOpportunity/action/11111111-1111-1111-1111-111111111111/verify");
    expect(init).toMatchObject({ method: "PUT", headers: { Authorization: "Bearer tok" } });
  });

  it("needs opportunity ids for live mode", () => {
    expect(() => yomaConfigFromEnv({ YOMA_MODE: "live" })).toThrow(/YOMA_OPPORTUNITY_IDS/);
  });
});
