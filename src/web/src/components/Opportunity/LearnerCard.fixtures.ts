import type { LearnerCard } from "~/api/models/courseQuality";

// Sample cards for the development-only preview page (/course-quality-preview).

const base: Omit<LearnerCard, "status" | "cost"> = {
  opportunityId: "00000000-0000-0000-0000-000000000001",
  format: "Course",
  dateStart: "2026-11-10T00:00:00Z",
  dateEnd: "2027-01-22T00:00:00Z",
  datesConfirmed: true,
  selfPaced: false,
  commitmentHours: 1008,
  timelineSummary: "6 weeks",
  highlights: [
    "Listing is live",
    "Course link works",
    "Dates checked",
    "Key details provided",
    "Format confirmed",
  ],
  dateAssessed: "2026-10-28T09:30:00Z",
};

export const LEARNER_CARD_FIXTURES: { name: string; card: LearnerCard }[] = [
  {
    name: "Verified, free",
    card: {
      ...base,
      status: "Verified",
      cost: {
        type: "Free",
        amount: null,
        currency: null,
        evidence: "This course is free to join.",
      },
    },
  },
  {
    name: "Webinar, verified and free",
    card: {
      ...base,
      format: "Webinar",
      status: "Verified",
      dateEnd: "2026-11-10T00:00:00Z",
      commitmentHours: 2,
      timelineSummary: "2 hours",
      cost: {
        type: "Free",
        amount: null,
        currency: null,
        evidence: "Free to attend.",
      },
    },
  },
  {
    name: "Unknown format (no label), needs review",
    card: {
      ...base,
      format: "Unknown",
      status: "NeedsReview",
      cost: { type: "Unknown", amount: null, currency: null, evidence: null },
    },
  },
  {
    name: "Verified, paid",
    card: {
      ...base,
      status: "Verified",
      commitmentHours: 40,
      timelineSummary: null, // hours alone must not show as the time needed
      cost: {
        type: "Paid",
        amount: 1450,
        currency: "ZAR",
        evidence: "Course fee: R1 450 once-off.",
      },
    },
  },
  {
    name: "Needs review, unknown cost",
    card: {
      ...base,
      datesConfirmed: false,
      status: "NeedsReview",
      highlights: ["Listing is live", "Course link works"],
      cost: { type: "Unknown", amount: null, currency: null, evidence: null },
    },
  },
  {
    name: "Verified by a reviewer, suspect dates (dates to be confirmed)",
    card: {
      ...base,
      datesConfirmed: false,
      status: "Verified",
      highlights: [
        "Listing is live",
        "Course link works",
        "Format confirmed",
        "Reviewed by the Yoma team",
      ],
      cost: {
        type: "Free",
        amount: null,
        currency: null,
        evidence: "This course is free to join.",
      },
    },
  },
  {
    name: "Not verified, no highlights",
    card: {
      ...base,
      status: "NotVerified",
      highlights: [],
      cost: { type: "Unknown", amount: null, currency: null, evidence: null },
    },
  },
  {
    name: "Missing timeline (no end date, hours or summary)",
    card: {
      ...base,
      status: "Verified",
      dateEnd: null,
      commitmentHours: null,
      timelineSummary: null,
      cost: {
        type: "Free",
        amount: null,
        currency: null,
        evidence: "No payment is required.",
      },
    },
  },
  {
    name: "Verified as self-paced (Start anytime, no dates)",
    card: {
      ...base,
      status: "Verified",
      dateStart: "2021-06-30T00:00:00Z",
      dateEnd: null,
      datesConfirmed: false,
      selfPaced: true,
      commitmentHours: 5,
      timelineSummary: "5 hours",
      highlights: [
        "Listing is live",
        "Course link works",
        "Key details provided",
        "Format confirmed",
        "Start anytime",
      ],
      cost: {
        type: "Free",
        amount: null,
        currency: null,
        evidence: "In this free course, you will learn",
      },
    },
  },
  {
    name: "Paid, a bare $ names no currency",
    card: {
      ...base,
      status: "Verified",
      cost: {
        type: "Paid",
        amount: 49,
        currency: null,
        evidence: "Price: $49",
      },
    },
  },
  {
    name: "Summary only, paid with no amount",
    card: {
      ...base,
      status: "Verified",
      dateEnd: null,
      commitmentHours: null,
      cost: {
        type: "Paid",
        amount: null,
        currency: null,
        evidence: "A registration fee applies.",
      },
    },
  },
  {
    name: "Free without evidence (must read as not confirmed)",
    card: {
      ...base,
      status: "NeedsReview",
      cost: { type: "Free", amount: null, currency: null, evidence: null },
    },
  },
];
