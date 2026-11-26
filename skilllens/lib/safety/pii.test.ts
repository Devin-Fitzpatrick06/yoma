import { describe, expect, it } from "vitest";
import { redactPii } from "./pii";

describe("redactPii", () => {
  it.each([
    ["Call me on 08031234567 tomorrow", "phone"],
    ["My number is +234 803 123 4567.", "phone"],
    ["0803-123-4567", "phone"],
    ["email amina.b@gmail.com please", "email"],
    ["Come to No. 12 Zoo Road", "address"],
    ["I live at Gwammaja quarters behind the mosque.", "address"],
    ["My name is Musa Bello and I sell rice", "name"],
    ["my name na Chidi", "name"],
    ["follow @kano_tomatoes", "handle"],
  ])("removes %s", (input, kind) => {
    const r = redactPii(input);
    expect(r.found).toContain(kind);
    expect(r.text).toContain(`[${kind}]`);
  });

  it("keeps the learner's money reasoning and the scenario's fictional names", () => {
    const answer = "Amina should buy 20 baskets at ₦500, that is ₦10,000, and sell 3 baskets on rainy days. Profit 2,500.";
    expect(redactPii(answer)).toEqual({ text: answer, found: [] });
  });

  it("keeps the words around a redacted name or address", () => {
    expect(redactPii("My name is Musa Bello. I think").text).toBe("My name is [name]. I think");
    expect(redactPii("I live at Gwammaja, Kano").text).toBe("I live at [address], Kano");
  });
});
