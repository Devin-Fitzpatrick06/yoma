// The three sides of the Course Quality demo, for the view switcher. Pure, so it is unit tested.

export type DemoSide = "learner" | "reviewer" | "partner";

export const DEFAULT_DEMO_ORG_ID = "c0a7e000-0000-4000-8000-000000000001";

export interface DemoSideLink {
  side: DemoSide;
  label: string;
  caption: string;
  href: string;
}

export const demoSides = (
  organisationId: string = DEFAULT_DEMO_ORG_ID,
): DemoSideLink[] => [
  {
    side: "learner",
    label: "Learner",
    caption: "Courses with their learner card, verified first, then by grade",
    href: "/opportunities",
  },
  {
    side: "reviewer",
    label: "Reviewer",
    caption:
      "Courses the checks were unsure about, one at a time, and the rubric",
    href: "/admin/course-quality",
  },
  {
    side: "partner",
    label: "Partner",
    caption: "What to fix so a held-back course reaches more learners",
    href: `/organisations/${encodeURIComponent(organisationId)}/course-quality`,
  },
];

/** The side a route belongs to, from the router's pathname (`/organisations/[id]/...`) or a path. */
export const demoSideOf = (pathname: string): DemoSide | null => {
  const path = pathname.split(/[?#]/)[0] ?? "";
  if (path === "/opportunities" || path.startsWith("/opportunities/"))
    return "learner";
  if (
    path === "/admin/course-quality" ||
    path.startsWith("/admin/course-quality/")
  )
    return "reviewer";
  if (/^\/organisations\/[^/]+\/course-quality\/?$/.test(path))
    return "partner";
  return null;
};
