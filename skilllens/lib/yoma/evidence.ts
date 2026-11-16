// Turns a saved practice sample into what Yoma accepts. Field names match the Yoma API source:
// - MyOpportunityRequestVerify (PUT /api/v3/myOpportunity/action/{opportunityId}/verify, multipart, learner's own token)
// - MyOpportunityInfoCsvImport (POST /api/v3/myOpportunity/action/verify/csv, org admin bulk import)
import type { PracticeSample } from "../schema";
import { textPdf } from "./pdf";

export const PRACTICE_LABEL = "Practice sample, not a certification";

export function samplePdf(sample: PracticeSample, courseTitle: string): Uint8Array {
  return textPdf([
    [16, `SkillLens: ${courseTitle}`],
    [12, PRACTICE_LABEL],
    [10, `Saved ${sample.savedAt.slice(0, 10)}. AI feedback was checked against an educator-approved rubric. This is not a score or a certificate.`],
    [12, "Challenge"],
    [11, sample.scenario],
    [12, "Learner's answer (Level 2)"],
    [11, sample.answer],
    [12, "Skills practised"],
    [11, sample.skills.map((s) => s.name).join(", ")],
  ]);
}

/** Multipart body for Yoma's verify endpoint. Yoma attaches the opportunity's own skills when it is verified. */
export function verifyForm(sample: PracticeSample, courseTitle: string): FormData {
  const form = new FormData();
  const pdf = samplePdf(sample, courseTitle);
  form.append("Certificate", new Blob([pdf.slice().buffer], { type: "application/pdf" }), "skilllens-practice-sample.pdf");
  form.append("DateStart", sample.savedAt);
  form.append("DateEnd", sample.savedAt);
  form.append("Feedback", `${PRACTICE_LABEL}. SkillLens ${courseTitle}.`);
  return form;
}

export interface CsvLearner {
  email?: string;
  phoneNumber?: string;
  firstName?: string;
  surname?: string;
  gender?: string;
  country?: string;
}

const CSV_HEADER = ["Email", "PhoneNumber", "FirstName", "Surname", "Gender", "Country", "DateCompleted", "OpportunityExternalId"];
const cell = (v = "") => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v);

/**
 * Rows for an org admin's CSV import. SkillLens never holds learner contact details: P2E supplies them at export time.
 * Yoma needs an email or a phone number to find the learner.
 */
export function verifyCsv(rows: { learner: CsvLearner; sample: PracticeSample; opportunityExternalId: string }[]): string {
  const lines = rows.map(({ learner, sample, opportunityExternalId }) => {
    if (!learner.email && !learner.phoneNumber) throw new Error("Each CSV row needs an email or a phone number");
    return [learner.email, learner.phoneNumber, learner.firstName, learner.surname, learner.gender, learner.country, sample.savedAt.slice(0, 10), opportunityExternalId]
      .map(cell)
      .join(",");
  });
  return [CSV_HEADER.join(","), ...lines].join("\n") + "\n";
}
