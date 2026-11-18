import { describe, expect, it } from "vitest";
import { getRubric } from "./data";
import { evidenceFor, pickSuggestion } from "./evidence";

const fl = getRubric("fl-rubric");
const jr = getRubric("jr-rubric");
const crit = (r: typeof fl, id: string) => r.criteria.find((c) => c.id === id)!;
const challenge = "Amina sells tomatoes in Kano for ₦500 a basket. On rainy days sales drop.";

describe("evidenceFor", () => {
  it("reads numbers: none, money talk, amounts, a worked sum", () => {
    const n = crit(fl, "numbers");
    expect(evidenceFor(n, "She should try harder.", challenge).level).toBe(1);
    expect(evidenceFor(n, "She will lose money.", challenge).level).toBe(2);
    expect(evidenceFor(n, "She could save ₦1,000.", challenge).level).toBe(3);
    expect(evidenceFor(n, "4 baskets x ₦500 = ₦2,000 lost, more than she earns.", challenge).level).toBe(4);
  });

  it("reads steps: vague, one concrete step, several with timing", () => {
    const a = crit(fl, "action");
    expect(evidenceFor(a, "I don't know.", challenge).level).toBe(1);
    expect(evidenceFor(a, "She should do something different.", challenge).level).toBe(2);
    expect(evidenceFor(a, "She should buy fewer tomatoes on rainy days.", challenge).level).toBe(3);
    expect(evidenceFor(a, "First, she should check the weather every morning. Then she can buy fewer baskets and take WhatsApp orders.", challenge).level).toBe(4);
  });

  it("reads polite wording and exact quoted words", () => {
    const c = crit(jr, "communication");
    expect(evidenceFor(c, "Go to work.", "").level).toBe(1);
    expect(evidenceFor(c, "Be nice to the manager.", "").level).toBe(2);
    expect(evidenceFor(c, "She should say sorry to the manager.", "").level).toBe(3);
    expect(evidenceFor(c, "She should text: “Good morning sir, I am sorry, I will be 30 minutes late because of traffic.”", "").level).toBe(4);
  });

  it("counts the course's own tools, including Pidgin-free terms like STAR", () => {
    const t = crit(jr, "tools");
    expect(evidenceFor(t, "Just go there.", "").level).toBe(1);
    expect(evidenceFor(t, "Use the STAR method, research the company and follow up after.", "").level).toBe(4);
    expect(evidenceFor(crit(fl, "tools"), "Use the 50:30:20 rule and pay yourself first.", "").level).toBe(3);
  });
});

describe("pickSuggestion", () => {
  it("picks the lowest criterion, but not the one the last tip fixed", () => {
    expect(pickSuggestion(fl, { cause: 3, tools: 2, numbers: 2, action: 3 })).toBe("tools");
    const previous = { answer: "x", levels: { cause: 3, tools: 2, numbers: 1, action: 3 }, suggestionCriterionId: "numbers" };
    expect(pickSuggestion(fl, { cause: 3, tools: 2, numbers: 2, action: 3 }, previous)).toBe("tools");
    expect(pickSuggestion(fl, { cause: 3, tools: 3, numbers: 1, action: 3 }, { ...previous, levels: { ...previous.levels, tools: 3 } })).toBe("numbers");
  });
});
