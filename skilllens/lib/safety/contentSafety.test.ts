import { afterEach, describe, expect, it, vi } from "vitest";
import { createAzureSafety, createModelSafety, getSafety, stubSafety } from "./contentSafety";

afterEach(() => vi.unstubAllGlobals());

describe("stub safety", () => {
  it("flags clearly harmful text and passes normal answers", async () => {
    expect((await stubSafety.checkText("I want to kill myself")).categories[0].category).toBe("SelfHarm");
    expect((await stubSafety.checkText("Amina should sell to regular customers")).flagged).toBe(false);
  });
});

describe("Azure Content Safety", () => {
  const safety = createAzureSafety({ endpoint: "https://cs.cognitiveservices.azure.com", key: "secret", threshold: 2 });

  it("calls text:analyze and flags categories at or above the threshold", async () => {
    const fetchMock = vi.fn(async () =>
      Response.json({ categoriesAnalysis: [{ category: "Hate", severity: 0 }, { category: "Violence", severity: 2 }] }),
    );
    vi.stubGlobal("fetch", fetchMock);
    expect(await safety.checkText("text")).toEqual({ flagged: true, categories: [{ category: "Violence", severity: 2 }] });
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("https://cs.cognitiveservices.azure.com/contentsafety/text:analyze?api-version=2024-09-01");
    expect((init.headers as Record<string, string>)["Ocp-Apim-Subscription-Key"]).toBe("secret");
  });

  it("fails closed when the service errors", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response("down", { status: 503 })));
    await expect(safety.checkImage(new Uint8Array([1]))).rejects.toThrow(/503/);
  });
});

describe("getSafety", () => {
  it("defaults to the stub and names missing Azure settings", () => {
    expect(getSafety({})).toBe(stubSafety);
    expect(() => getSafety({ CONTENT_SAFETY_PROVIDER: "azure" })).toThrow(/CONTENT_SAFETY_ENDPOINT, CONTENT_SAFETY_KEY/);
    expect(() => getSafety({ CONTENT_SAFETY_PROVIDER: "azure", CONTENT_SAFETY_ENDPOINT: "x", CONTENT_SAFETY_KEY: "k", CONTENT_SAFETY_THRESHOLD: "3" })).toThrow(/2, 4 or 6/);
  });
});

describe("model safety", () => {
  const config = { baseUrl: "http://localhost:11434/v1", apiKey: "local", chatModel: "m", visionModel: "m", label: "Local model" };
  const reply = (content: unknown) => vi.fn(async () => Response.json({ choices: [{ message: { content: JSON.stringify(content) } }] }));

  it("uses the model when the keywords pass, and flags at severity 2 or more", async () => {
    const fetchMock = reply({ Hate: 0, SelfHarm: 2, Sexual: 0, Violence: 0 });
    vi.stubGlobal("fetch", fetchMock);
    expect(await createModelSafety(config).checkText("Nobody would miss me")).toEqual({ flagged: true, categories: [{ category: "SelfHarm", severity: 2 }] });
    const sent = JSON.parse((fetchMock.mock.calls[0] as unknown as [string, RequestInit])[1].body as string);
    expect(sent.messages[1].content).toContain("<<<\nNobody would miss me\n>>>");
  });

  it("skips the model when the keyword list already flags", async () => {
    const fetchMock = reply({});
    vi.stubGlobal("fetch", fetchMock);
    expect((await createModelSafety(config).checkText("I want to kill myself")).flagged).toBe(true);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("fails closed when the model is down", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));
    await expect(createModelSafety(config).checkText("hello there friend")).rejects.toThrow(/could not be reached/);
  });

  it("needs a real AI provider", () => {
    expect(() => getSafety({ CONTENT_SAFETY_PROVIDER: "model" }, null)).toThrow(/real AI_PROVIDER/);
  });
});
