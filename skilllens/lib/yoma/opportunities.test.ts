import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchOpportunities, opportunitiesFor } from "./opportunities";

afterEach(() => vi.unstubAllGlobals());

const item = (id: string, title: string) => ({ id, title, type: "Job", organizationName: "Jobberman - Nigeria", yomaInfoURL: `https://stage.yoma.world/opportunities/${id}` });

describe("Yoma opportunities", () => {
  it("searches once per keyword, drops test data and duplicates, keeps four", async () => {
    const pages = [
      [item("1", "Sales Officer in Lagos"), item("2", "Test CSV"), item("3", "Empty pool - Testing")],
      [item("1", "Sales Officer in Lagos"), item("4", "Retail Assistant"), item("5", "Bookkeeper"), item("6", "Cashier"), item("7", "Driver")],
    ];
    const fetchMock = vi.fn(async () => Response.json({ items: pages.shift() ?? [], totalCount: 0 }));
    const list = await fetchOpportunities("job-readiness", "https://v3api.stage.yoma.world/api/v3", fetchMock as unknown as typeof fetch);
    expect(list.map((o) => o.id)).toEqual(["1", "4", "5", "6"]); // "Lagos" does not say Nigeria, so Yoma order holds
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe("https://v3api.stage.yoma.world/api/v3/opportunity/search");
    expect(JSON.parse(init.body as string)).toMatchObject({ pageNumber: 1, valueContains: "work readiness" });
  });

  it("falls back to the snapshot when Yoma is unreachable, and says so", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => { throw new TypeError("fetch failed"); }));
    const list = await opportunitiesFor("financial-literacy", { YOMA_API_BASE_URL: "https://unreachable.example/api/v3" });
    expect(list.source).toBe("snapshot");
    expect(list.items.length).toBeGreaterThan(0);
  });

  it("uses only the snapshot when told to", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    expect((await opportunitiesFor("job-readiness", { YOMA_OPPORTUNITIES: "snapshot" })).source).toBe("snapshot");
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
