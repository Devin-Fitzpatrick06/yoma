// What a course's category adds to its learner card: one of six colour families, and the fields
// and example roles it could lead towards. Keyed by category name, so a category this list does
// not know reads as Other. Green stays out of the families: it marks verification.

export interface CategoryColour {
  band: string;
  bandText: string;
}

const CORAL: CategoryColour = { band: "bg-pink", bandText: "text-black" };
const SKY: CategoryColour = { band: "bg-blue", bandText: "text-black" };
const AMBER: CategoryColour = { band: "bg-orange", bandText: "text-black" };
const SAND: CategoryColour = {
  band: "bg-yellow-light",
  bandText: "text-black",
};
const PLUM: CategoryColour = { band: "bg-purple", bandText: "text-white" };
const LILAC: CategoryColour = {
  band: "bg-purple-light",
  bandText: "text-black",
};

interface CategoryTheme {
  colour: CategoryColour;
  fields: string[];
  roles: string[];
}

const OTHER: CategoryTheme = {
  colour: LILAC,
  fields: ["Community work", "General employment"],
  roles: ["Community volunteer", "Project assistant", "Field worker"],
};

const CATEGORY_THEMES: Record<string, CategoryTheme> = {
  "Agriculture, Food, Environment and Climate": {
    colour: SAND,
    fields: ["Farming and food", "Environment and climate"],
    roles: ["Farm assistant", "Agri-entrepreneur", "Conservation worker"],
  },
  "Beauty & Personal Care": {
    colour: CORAL,
    fields: ["Beauty and wellness", "Salon services"],
    roles: ["Hair stylist", "Beauty therapist", "Makeup artist"],
  },
  "Business, Finance & Marketing": {
    colour: AMBER,
    fields: ["Business and entrepreneurship", "Finance and marketing"],
    roles: ["Small business owner", "Sales assistant", "Marketing assistant"],
  },
  "Creative, Media & Design": {
    colour: PLUM,
    fields: ["Creative industries", "Media and design"],
    roles: ["Graphic designer", "Content creator", "Video editor"],
  },
  "Education & Teaching": {
    colour: LILAC,
    fields: ["Education", "Youth work"],
    roles: ["Teaching assistant", "Tutor", "Youth facilitator"],
  },
  "Engineering, Science & Mathematics": {
    colour: SKY,
    fields: ["Engineering", "Science and technical trades"],
    roles: ["Technician", "Lab assistant", "Installer"],
  },
  "Health, Safety & Wellbeing": {
    colour: CORAL,
    fields: ["Health and care", "Workplace safety"],
    roles: ["Care worker", "Community health worker", "Safety officer"],
  },
  "History, Society & Human Rights": {
    colour: PLUM,
    fields: ["Civil society", "Heritage and culture"],
    roles: ["Community advocate", "NGO assistant", "Heritage guide"],
  },
  "Hospitality & Tourism": {
    colour: SAND,
    fields: ["Hospitality", "Tourism"],
    roles: ["Front desk agent", "Tour guide", "Food service attendant"],
  },
  "Languages & Communication": {
    colour: LILAC,
    fields: ["Communication", "Customer-facing work"],
    roles: ["Customer service agent", "Translator", "Communications assistant"],
  },
  "Law, Governance & Compliance": {
    colour: AMBER,
    fields: ["Law and compliance", "Public administration"],
    roles: ["Paralegal", "Compliance assistant", "Government clerk"],
  },
  "Office, Admin & Professional Skills": {
    colour: AMBER,
    fields: ["Office administration", "Professional services"],
    roles: ["Administrative assistant", "Receptionist", "Office coordinator"],
  },
  Other: OTHER,
  "Personal Development & Career Readiness": {
    colour: LILAC,
    fields: ["Early career", "Any workplace"],
    roles: ["Intern", "Trainee", "Graduate apprentice"],
  },
  "Retail & Food Services": {
    colour: SAND,
    fields: ["Retail", "Food services"],
    roles: ["Shop assistant", "Cashier", "Barista"],
  },
  "Technology, AI & Data": {
    colour: SKY,
    fields: ["Technology", "AI and data"],
    roles: ["Junior data analyst", "IT support technician", "Junior developer"],
  },
};

// A course's skills can name a role its category is too broad to reach
const SKILL_ROLES: { skill: RegExp; role: string }[] = [
  { skill: /data analy|spreadsheet/i, role: "Data analyst" },
  { skill: /python|coding|programming/i, role: "Junior developer" },
  { skill: /artificial intelligence|\bAI\b/i, role: "AI operations assistant" },
  { skill: /digital marketing/i, role: "Digital marketer" },
  { skill: /graphic design/i, role: "Graphic designer" },
  { skill: /bookkeeping|accounting/i, role: "Bookkeeper" },
  { skill: /solar|electrical/i, role: "Solar installer" },
  { skill: /customer service/i, role: "Customer service agent" },
];

const MAX_ROLES = 3;

interface Named {
  name: string;
}

const getTheme = (categories?: Named[] | null): CategoryTheme =>
  CATEGORY_THEMES[categories?.[0]?.name ?? ""] ?? OTHER;

/** A course with no category reads as Other, so its band still has a colour. */
export const getCategoryColour = (
  categories?: Named[] | null,
): CategoryColour => getTheme(categories).colour;

export const getCategoryName = (categories?: Named[] | null): string =>
  categories?.[0]?.name ?? "Other";

/** Skill-specific roles first, then the category's, without repeats. */
export const getCareerPaths = (
  categories?: Named[] | null,
  skills?: Named[] | null,
): { fields: string[]; roles: string[] } => {
  const theme = getTheme(categories);
  const fromSkills = SKILL_ROLES.filter(({ skill }) =>
    skills?.some(({ name }) => skill.test(name)),
  ).map(({ role }) => role);

  // "Data analyst" from a skill replaces the category's "Junior data analyst"
  const fromCategory = theme.roles.filter(
    (role) =>
      !fromSkills.some((skillRole) =>
        role.toLowerCase().includes(skillRole.toLowerCase()),
      ),
  );

  return {
    fields: theme.fields,
    roles: [...new Set([...fromSkills, ...fromCategory])].slice(0, MAX_ROLES),
  };
};
