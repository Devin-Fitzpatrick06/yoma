import { beforeEach, describe, expect, it } from "vitest";
import { learner } from "./shared";
import { issueCredential, registerPass, resetYoid, thread } from "./yoid";

beforeEach(resetYoid);

describe("mocked YoID thread", () => {
  it("lists the four handoff items in journey order, with the Skill Credential waiting until SkillLens writes it", () => {
    const items = thread(learner.yoidId);
    expect(items.map((r) => `${r.item}:${r.refId}`)).toEqual(["Saved Opportunity:opp-job-001", "Learning Plan:plan-001", "Skill Credential:cred-002", "Job Application:app-001"]);
    expect(items.map((r) => r.state)).toEqual(["recorded", "recorded", "waiting", "next"]);
  });

  it("records cred-002 once, only for a pass the server scored, and returns the updated thread", () => {
    expect(registerPass("chal-001", { diagnosis: 3, tools: 4, order: 2 })).toBeNull(); // not every part Good
    const passId = registerPass("chal-001", { diagnosis: 3, tools: 4, order: 3 })!;
    const out = issueCredential(learner.yoidId, passId);
    expect("credential" in out && out.credential).toMatchObject({ id: "cred-002", title: "Solar PV Installation Basics: practical challenge passed", skills: ["Solar panel installation"], status: "verified (mocked)" });
    expect("thread" in out && out.thread[2]).toMatchObject({ state: "recorded", note: "Recorded by SkillLens (mocked write)" });
    // A retry with the same pass returns the same credential, not a second one or an error.
    const again = issueCredential(learner.yoidId, passId);
    expect("credential" in again && again.credential).toEqual("credential" in out && out.credential);
  });

  it("refuses an unknown YoID or a made-up pass id", () => {
    const passId = registerPass("chal-001", { diagnosis: 4, tools: 4, order: 4 })!;
    expect(issueCredential("yoid-someone-else", passId)).toEqual({ error: "Unknown YoID" });
    expect(issueCredential(learner.yoidId, "00000000-0000-4000-8000-000000000000")).toEqual({ error: "No passed challenge to record" });
    // A tampered pass (another challenge's id, old signature) is refused.
    const [payload, sig] = passId.split(".");
    const forged = Buffer.from(JSON.stringify({ ...JSON.parse(Buffer.from(payload, "base64url").toString()), t: "fl-loan-offer" })).toString("base64url");
    expect(issueCredential(learner.yoidId, `${forged}.${sig}`)).toEqual({ error: "No passed challenge to record" });
  });

  it("never issues a pass for the other sample challenges, so no unknown ids reach Halima's YoID", () => {
    expect(registerPass("fl-loan-offer", { cause: 3, tools: 3, numbers: 4, action: 3 })).toBeNull();
  });
});
