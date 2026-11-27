# Iteration 3: learning path planner, and the rest of the iteration-2 feedback

Status: in-progress. Branch `feature/course-quality-judger`, uncommitted on top of `5667e2fb`. The project lead approves
commits. Written 2026-11-23 as the shared spec for an overnight multi-engineer build.

## What the project lead asked for

1. **Learning path planner** (feedback #4, pathway view, made learner-facing):
   - a user-specific landing page: questions about interests and career, not the opportunities list;
   - after answering, a separate tab, like Opportunities, with a mapped-out path of **8** courses;
   - fun UX: button pop-ups, slides, cards that shift into place and lock in.
   - The metrics that choose the path are ours to decide (below).
2. **All five feedback points addressed:**
   - #1 better than the current system;
   - #2 partner badging;
   - #3 outcome data in the rubric;
   - #4 pathway view;
   - #5 beyond learning courses (jobs).

## Ground rules for every engineer

- Nothing is committed. Do not run `git commit`, `git push`, `git stash`, `git checkout` or `git reset`.
- No new npm or NuGet dependencies. Animation is CSS (keyframes, transitions, View Transitions where supported) and React state.
- Never read `env.secrets`. Do not touch `helm/` or `.github/workflows/`.
- Yoma's fonts and colours only (Tailwind theme tokens already in `src/web`). Mobile first. Respect `prefers-reduced-motion`.
- Off by default in production:
  - **API:** `CourseQuality:Pathway:Enabled` and `CourseQuality:PartnerBadges:Enabled` default to `false` in `appsettings.json`.
  - **Web:** the planner is behind `NEXT_PUBLIC_PATHWAY_ENABLED`, default off.
  - **Demo:** `scripts/course-quality-demo` turns both on. That folder is owned by the lead session, not by the engineers.
  - **Why:** web is Jason's area, and visible changes need his design approval before they ship.
- Follow the existing CourseQuality patterns: options classes, `ValidateAndThrow`, repositories in `Yoma.Core.Infrastructure.Database`, EF migrations named `ApplicationDb_CourseQuality_*`, xUnit tests under `src/test/Yoma.Core.Test/CourseQuality`.
- API checks (run from `src/api`):
  - `mise x -- dotnet build Yoma.Core.sln` with 0 warnings;
  - `mise x -- src/test/Yoma.Core.Test/bin/Debug/net10.0/Yoma.Core.Test -namespace Yoma.Core.Test.CourseQuality`. `dotnet test` does not work on .NET 10 MTP.
  - `mise x -- dotnet format Yoma.Core.sln --verify-no-changes --include <changed folders>`.
- Web checks (run from `src/web`):
  - `mise x -- pnpm exec tsc --noEmit`;
  - `mise x -- pnpm lint`;
  - `mise x -- pnpm test`.
- Questions for the lead are never asked mid-run. Pick the sensible default, build it so the answer is a one-line config change, and return the question in your final report.

## #4 Learning path planner

### Learner flow

1. **Landing.** With the flag on, `/` sends a visitor to `/pathway/start` if they have no saved path, and to `/pathway` if they do. A visitor who wants the old home page can still reach it from a link on the start page.
2. **`/pathway/start`.** The questions come one per slide:
   - big chip buttons that pop (scale and bounce) when chosen;
   - a progress bar, and Back and Next controls;
   - each slide slides in from the right.
   - The last slide shows a short "building your path" animation, then routes to `/pathway`.
3. **`/pathway`, the "My Path" tab** in the main nav, next to Opportunities:
   - **Layout:** a vertical path (horizontal stepping on wide screens) of 8 course cards in 3 stages: Foundations, Build skills, Apply.
   - **Entrance:** cards fly in one by one and snap onto the path line with a short "lock" animation and a lock icon.
   - **Lock and unlock:** tapping the lock pins a course.
   - **Swap:** opens a pop-up sheet with up to 3 alternatives that slide in. Picking one animates it into place. Locked steps keep their position when the path is rebuilt.
   - **Progress:** each card shows its progress state: planned, started or done. A done step plays a small celebration. Progress is read from the learner's own completions when signed in.
   - **Explanation:** a "why this course" pop-up per card shows the reasons and factor bars.
   - **End of the path:** a "Where this leads" panel with up to 3 matching jobs.
   - **Change answers:** this re-opens the questions.
4. **Saving.**
   - **Signed out:** answers and the path live in `localStorage`.
   - **Signed in:** they are saved through `PUT /pathway/me`, and a signed-out path is uploaded on first sign-in.

### Questions

The API serves the questions (`GET /pathway/questions`), so admins can edit them later. The defaults:

| Key | Kind | Question | Options |
|---|---|---|---|
| `interests` | multi, up to 3 | What are you into? | the 9 course categories, with friendly labels and an emoji each |
| `career` | single | Where do you want to end up? | Software and web developer, Data analyst, Digital marketer, Start my own business, Finance and accounting, Health and care, Green and agriculture jobs, Creative and design, Hospitality and tourism, Not sure yet |
| `level` | single | How much do you know already? | New to this, I know the basics, I'm fairly confident |
| `hours` | single | How much time a week? | 1–2 hours, 3–5 hours, 6–10 hours, 10+ hours |
| `cost` | single | Free courses only? | Free only, Paid is fine too |
| `goal` | single | What matters most right now? | Get a job, Earn a certificate, Grow a business, Just exploring |

Each career maps to course categories and keywords in `CourseQuality:Pathway:Careers` (options, with defaults in code).

### How a path is chosen (rubric `pathway-v1`, weights in options)

**Hard filters.** A course is left out if it is:
- not a published Learning opportunity;
- `NotVerified` or `NeedsReview`;
- expired;
- paid, when the learner chose "Free only";
- not available in the learner's country (worldwide counts as available), when the country is known;
- excluded by the request.

**Step score** (0–100, weighted):

| Factor | Weight | Score |
|---|---|---|
| Fit | 35 | Category in the chosen interests: 60. A career keyword or skill matched in the title, skills or keywords: up to 40. |
| Quality | 25 | Grade A 100, B 80, C 55, D 30; Verified but ungraded 50. |
| Finishability | 15 | Smoothed completion rate `(completed + 2) / (started + 5)`, averaged with time fit. A course that fits about four weeks of the chosen hours scores 100, falling for longer courses. |
| Progression | 15 | The new course grade factor (#3): learners who finish this course and then go on. Neutral 50 without data. |
| Access | 10 | Free 100, paid 60 (when paid is allowed); language matches. |

**Building the 8.**
- Take the top 60 candidates by score. Pick greedily: each next pick maximises its score plus 0.3 × the share of new skills it adds.
- **Limits:** at most 2 from one organisation and at most 3 from one category. Locked steps are placed first.
- **Order:** by difficulty (Beginner, then Intermediate, then Advanced, then unknown), ties by hours, shortest first.
- **Stages:** steps 1–3 are Foundations, 4–6 Build skills, 7–8 Apply. A "fairly confident" learner skips beginner-only courses where alternatives exist.
- **Fewer than 8:** if fewer than 8 courses pass, return what passed and say so in `summary`.
- **Determinism:** the same answers on the same data give the same path.

**Reasons.** Every step carries plain sentences, for example:
- "Matches your interest in Technology, AI and Data";
- "Grade A: one of the best-rated courses";
- "About 4 hours, which fits 3–5 hours a week";
- "Free";
- "Learners who finish this often go on to a next course".

**Where this leads.** Up to 3 published, Verified jobs whose category, title or keywords match the career. They are ranked by job grade and filtered to the learner's country when it is known. The list is empty if jobs are off.

### API contract

Controller `CourseQualityPathwayController`, route prefix `api/v3/courseQuality/pathway`. The JSON is camelCase, like the other CourseQuality endpoints. Everything returns 404 while `CourseQuality:Pathway:Enabled` is false.

| Method and path | Auth | Body or query | Returns |
|---|---|---|---|
| `GET /questions` | anonymous | none | `PathwayQuestion[]`: `{ key, title, subtitle?, kind: "Single" \| "Multi", maxSelections, options: [{ value, label, emoji? }] }` |
| `POST /recommend` | anonymous | `PathwayRequest`: `{ answers: Record<string, string[]>, countryCodeAlpha2?, lockedSteps?: [{ position, opportunityId }], excludeIds?: Guid[] }` | `PathwayResult` |
| `POST /alternatives` | anonymous | `{ request: PathwayRequest, position, currentIds: Guid[] }` | `PathwayStep[]`, up to 3, with none from `currentIds` |
| `GET /me` | signed in | none | `PathwayProfile` or 204 |
| `PUT /me` | signed in | `{ answers, steps: [{ position, opportunityId, locked }] }` | `PathwayProfile` |
| `GET /funnel` | admin | none | `PathwayFunnel` |

**`PathwayResult`:**
- `{ rubricVersion: "pathway-v1", generatedAt, summary, stages: [{ key: "Foundations" | "Build" | "Apply", title, positions: number[] }], steps: PathwayStep[], nextJobs: PathwayJob[], totalHours?, skillsCovered: string[] }`.

**`PathwayStep`:**
- `{ position, stage, opportunityId, title, organizationName, organizationLogoURL?, url, grade?: "A" | "B" | "C" | "D", qualityStatus, hours?, costType?, difficulty?, newSkills: string[], score, factors: [{ key, label, score, weight, hasData }], reasons: string[], locked, progress: "Planned" | "Started" | "Done" }`.
- `url` is the Yoma page `/opportunities/{id}`.
- `progress` is only filled in for a signed-in learner, from their MyOpportunity records (verification completed means Done; viewed or navigated means Started).

**`PathwayJob`:**
- `{ opportunityId, title, organizationName, countryName?, grade? }`.

**`PathwayProfile`:**
- `{ answers, steps: PathwayStep[], dateModified }`.
- `GET /me` re-hydrates the saved step ids with current data and progress. A saved step that is no longer eligible is still returned, with `reasons` saying why.

**`PathwayFunnel`, for #4's original admin view:**
- `{ profiles, positions: [{ position, planned, started, done, doneShare, flagged }], courses: [{ opportunityId, title, inPaths, started, done, flagged }] }`.
- A position or course is `flagged` when its done share is under half the average for that position, with at least 5 paths.

**Storage.** A new table `"CourseQuality"."PathwayProfile"`:
- columns: `Id`, `UserId` (unique), `Answers` (text JSON), `Steps` (text JSON of position, opportunityId and locked), `RubricVersion`, `DateCreated`, `DateModified`;
- created through an EF migration.

## #3 Outcome data in the rubric: Progression factor (Phase A)

- **New course grade factor** `Progression` (default weight 10; the other weights are scaled so the total stays 100). The course grade version moves forward one step.
- **Score:** of learners who completed this course, the share who completed another Learning course within 180 days that is either:
  - in the same category with a higher difficulty; or
  - a later step in a learner's saved path.
- It is compared with the category's average share: the average maps to 50 and twice the average or more to 100.
- **Privacy floor:** at least 5 completers, otherwise neutral 50 and `HasData = false`. Only aggregates are stored.
- **Wording:** "linked to", never "causes".
- Shown in the grade breakdown and on the rubric page like the other factors. Its weight is editable.
- **Phase B (employment credentials)** is not built. It is documented as next, pending data and consent.

## #2 Partner badging

- **Computation:** per organisation, from the latest course grades. Options live in `CourseQuality:PartnerBadges`:
  - `Enabled` (false);
  - `MinGradedCourses` (5);
  - `MinAShare` (0.3);
  - `MinABShare` (0.6).
- **Badge "Top pick partner":** earned when the organisation has at least `MinGradedCourses` graded courses, at least `MinAShare` of them are A, and at least `MinABShare` are A or B.
- **Review date:** each badge carries `reviewDue`, the start of the next quarter, and is recomputed with each regrade.
- **API:**
  - `GET api/v3/courseQuality/organization/{id}/badge`, for org admins and admins: `{ earned, label, gradedCourses, aCount, abCount, aNeeded, abNeeded, progressText, reviewDue }`. For example, the progress text reads "2 more A courses to earn this".
  - `GET api/v3/courseQuality/badges`, admin: every organisation's badge state, for the reviewer pages.
- **Web:**
  - the partner course-quality page shows the badge or the progress toward it;
  - the admin course-quality overview lists the badged partners;
  - learner-facing badges are not shown yet, because they wait for the hand-rated grade check and Jason's design.
- Partner emails about badges are not sent. They need separate sign-off.

## #5 Beyond learning courses (jobs)

- Job checks and job grades are already built (handoffs `2026-11-20-a.md` and `2026-11-23-a.md`).
- **Reviewer queue:** a type filter (All, Courses, Jobs), sent as `opportunityType`, which the API already supports.
- **Labels:** jobs show "Job" and the job grade factors in the breakdown.
- The lead session handles the demo import of live jobs.

## #1 Better than the current system

A write-up in this folder, `comparison.md`, comparing Yoma's current opportunity review with Zeki. It's built from the code, not guesses:
- what each checks;
- who does the work, and how much reviewer time it takes;
- what partners and learners see;
- where Zeki slots into the existing admin flow;
- which current steps it could take over.

## Ownership for the overnight build

| Lane | Owns | Must not touch |
|---|---|---|
| API grading | Progression factor, partner badges: grading defaults, engine, service, grade contract, `CourseQualityController`, `CourseQualityOptions` (badge options), their tests | pathway files, web |
| API pathway | `CourseQualityPathway*` models, service, interface, options, the new controller, repository, entity, migration, DI registration, tests | grading engine internals (read-only), web |
| Web planner | `src/web/src/pages/pathway/**`, `src/components/Pathway/**`, `src/api/models/pathway.ts`, `src/api/services/pathway.ts`, the Navbar "My Path" tab, the `/` redirect, `env.mjs` flag | admin and partner pages, `courseQuality.ts` |
| Web admin | review queue type filter, partner badge on the partner page, admin badge list, pathway funnel admin page, Progression factor label, `courseQuality.ts`, `courseGrading.ts` | pathway learner pages, Navbar |
| Writer | `comparison.md` | code |
