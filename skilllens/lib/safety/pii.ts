// Strips personal details from learner text before it reaches a model, a log or a saved sample.
// Learners are minors, so this runs on every answer and every photo transcription.
// shortcut: regex rules catch common formats (Nigerian phones, emails, street addresses, "my name is X");
// add a named-entity model (e.g. Azure AI Language PII) before scaling beyond the pilot.

export type PiiKind = "email" | "phone" | "address" | "name" | "handle";

const RULES: { kind: PiiKind; re: RegExp; keep?: number }[] = [
  { kind: "email", re: /[\w.+-]+@[\w-]+\.[\w.-]+/g },
  // +234 803 123 4567, 08031234567, 0803-123-4567, or any run of 10+ digits
  { kind: "phone", re: /\+?\d[\d\s().-]{8,}\d/g },
  // "No. 12 Zoo Road", "15 Murtala Mohammed Way"
  {
    kind: "address",
    re: /\b(?:no\.?\s*)?\d{1,4}[a-z]?,?\s+(?:[A-Za-z'-]+\s+){1,4}(?:street|st|road|rd|avenue|ave|close|crescent|way|lane|drive|estate)\b\.?/gi,
  },
  // "I live at <anything up to punctuation>"
  { kind: "address", re: /\b((?:[Ii] (?:live|stay|dey stay)|[Mm]y (?:house|address) (?:is|dey)) (?:at|for|on)\s+)[^.,;!?\n]+/g, keep: 1 },
  // Learner's own name: "my name is Musa Bello", "my name na Musa", "call me Musa"
  { kind: "name", re: /\b((?:[Mm]y name (?:is|na)|[Ii] am called|[Cc]all me)\s+)[A-Z][\w'-]*(?:\s+[A-Z][\w'-]*){0,2}/g, keep: 1 },
  { kind: "handle", re: /(^|\s)@\w{3,}/g, keep: 1 },
];

export interface Redaction {
  text: string;
  found: PiiKind[];
}

export function redactPii(input: string): Redaction {
  const found = new Set<PiiKind>();
  let text = input;
  for (const { kind, re, keep } of RULES) {
    text = text.replace(re, (match, ...groups) => {
      if (kind === "phone" && match.replace(/\D/g, "").length < 10) return match; // prices and quantities stay
      found.add(kind);
      return `${keep ? groups[keep - 1] : ""}[${kind}]`;
    });
  }
  return { text, found: [...found] };
}
