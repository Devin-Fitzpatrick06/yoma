import { describe, expect, it } from "vitest";
import { courses, examplesFor, getRubric, getTemplate, locale, rubricFor, samples, templates, templatesFor } from "./data";
import { challenge, credential, learner, opportunity, plan } from "./shared";
import { fillTemplate, slotValues, templateSlots } from "./template";

describe("synthetic seed data", () => {
  it("has 3 courses with 1-6 templates each (the shared course has exactly the one shared challenge), each using its course's rubric", () => {
    expect(courses).toHaveLength(3);
    for (const course of courses) {
      const list = templatesFor(course.id);
      expect(list.length).toBeGreaterThanOrEqual(1);
      expect(list.length).toBeLessThanOrEqual(6);
      for (const t of list) expect(getRubric(t.rubricId).courseId).toBe(course.id);
    }
  });

  it("keeps locale slot lists index-aligned (same length)", () => {
    const lengths = new Set(Object.values(locale.slots).map((l) => l.length));
    expect(lengths.size).toBe(1);
  });

  it("uses the same slots in every language of a template", () => {
    for (const t of templates) expect(templateSlots(t.text.pcm).sort()).toEqual(templateSlots(t.text.en).sort());
  });

  it("fills every template in every language for every seed", () => {
    for (const t of templates)
      for (const lang of ["en", "pcm"] as const)
        for (let seed = 0; seed < 5; seed++)
          expect(fillTemplate(t.text[lang], slotValues(locale, seed))).not.toMatch(/[{}]/);
  });

  it("has 20 scored samples per template: 10 prompt examples, 10 held-out", () => {
    for (const t of templates) {
      const criteria = getRubric(t.rubricId).criteria.map((c) => c.id).sort();
      const mine = samples.filter((s) => s.templateId === t.id);
      expect(mine).toHaveLength(20);
      expect(examplesFor(t.id)).toHaveLength(10);
      for (const s of mine) expect(Object.keys(s.scores).sort()).toEqual(criteria);
    }
  });
});

describe("challenges give learners what they need", () => {
  it("every challenge has a lesson recap, a way to start and its own course tools, filled for every seed", () => {
    for (const t of templates) {
      expect(t.recap && t.help && t.tools, t.id).toBeTruthy();
      for (const lang of ["en", "pcm"] as const)
        for (let seed = 0; seed < 5; seed++) {
          expect(fillTemplate(t.recap![lang], slotValues(locale, seed))).not.toMatch(/[{}]/);
          expect(fillTemplate(t.help![lang], slotValues(locale, seed))).not.toMatch(/[{}]/);
        }
    }
  });

  it("the profit challenge gives the price and both costs, and every snack still makes a profit", () => {
    const t = getTemplate("fl-ingredient-costs");
    const naira = (v: string) => Number(v.replace(/[^\d]/g, ""));
    for (let seed = 0; seed < 5; seed++) {
      const v = slotValues(locale, seed);
      const text = fillTemplate(t.text.en, v);
      for (const slot of ["snackPrice", "snackCostBefore", "snackCost"]) expect(text).toContain(v[slot]);
      expect(naira(v.snackCostBefore)).toBeLessThan(naira(v.snackCost));
      expect(naira(v.snackCost)).toBeLessThan(naira(v.snackPrice));
    }
  });

  it("the course-tools tip names the tools of this challenge, not another lesson's", () => {
    const tip = (id: string) => rubricFor(getTemplate(id)).criteria.find((c) => c.kind === "concepts")!;
    expect(tip("fl-ingredient-costs").improve.en).toContain("profit per item");
    expect(tip("fl-ingredient-costs").improve.en).not.toMatch(/50:30:20|budget/);
    expect(tip("fl-ingredient-costs").keywords).toContain("profit");
    expect(tip("jr-fee-scam").levels[2].pcm).not.toContain("{tools}");
  });
});

describe("fillTemplate", () => {
  it("throws on a slot with no value", () => {
    expect(() => fillTemplate("Hi {nobody}", {})).toThrow(/nobody/);
  });
});

describe("course order", () => {
  it("opens on the shared journey's course, then the two other sample courses", () => {
    expect(courses.map((c) => c.id)).toEqual(["solar-pv", "financial-literacy", "job-readiness"]);
    expect(courses[0].title.en).toBe(opportunity("opp-course-001")!.title);
  });
});

describe("BBS x GenU shared mock data", () => {
  it("uses the shared challenge word for word, after the shared course", () => {
    const t = getTemplate(challenge.id);
    expect(t.text.en).toBe(challenge.task);
    expect(t.courseId).toBe("solar-pv");
    expect(challenge.afterCourse).toBe("opp-course-001");
    expect(challenge.issues).toBe("cred-002");
  });

  it("has the shared learner, plan and credential with the ids every prototype uses", () => {
    expect(learner).toMatchObject({ id: "yp-001", yoidId: "yoid-ng-7f3a0123", displayName: "Halima Y." });
    expect(plan.id).toBe("plan-001");
    expect(plan.target).toBe("opp-job-001");
    expect(credential("cred-002")?.skills).toEqual(["solar-install"]);
  });
});
