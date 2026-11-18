import { describe, expect, it } from "vitest";
import { courses, examplesFor, getRubric, locale, samples, templates, templatesFor } from "./data";
import { fillTemplate, slotValues, templateSlots } from "./template";

describe("synthetic seed data", () => {
  it("has 2 courses with 4-6 templates each, each using its course's rubric", () => {
    expect(courses).toHaveLength(2);
    for (const course of courses) {
      const list = templatesFor(course.id);
      expect(list.length).toBeGreaterThanOrEqual(4);
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

describe("fillTemplate", () => {
  it("throws on a slot with no value", () => {
    expect(() => fillTemplate("Hi {nobody}", {})).toThrow(/nobody/);
  });
});

describe("P2E course order", () => {
  it("is 1 Financial Literacy, then 2 Job Readiness", () => {
    expect(courses.map((c) => c.id)).toEqual(["financial-literacy", "job-readiness"]);
    expect(courses.map((c) => c.title.en)).toEqual(["Financial Literacy", "Job Readiness"]);
  });
});
