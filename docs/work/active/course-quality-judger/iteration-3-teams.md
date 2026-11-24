# Iteration 3: My Yoma pathways, outcomes and badges (team plan)

Status: in-progress

Builds on `iteration-3.md` (feedback positions) and the approved prototype
(the clickable "My Yoma" demo shown to the project lead on 2026-11-24). Branch: `feature/course-quality-judger`.

## What ships

1. **My Yoma tab** next to Opportunities. First visit: five questions (goal, starting point, what you can
   already do, hours a week, where you want to work). Then "Your world, your Yoma": a YoID summary (what the
   youth has done), the goal, a path of 8–12 courses in four stages with a through line (each course says the
   skill it adds and why it sits there), live jobs at the end, learner badges.
2. **Career mapping and outcomes.** Goals are configuration (`Pathway:Goals`), not code. Per course: completers,
   progression rate (went on to another course in the goal), employment rate (completed a Job opportunity or
   holds an employment credential afterwards), continuation from the previous step, and a drop-off flag.
   Outcomes join the grading rubric as a factor at weight 0 until data is sufficient.
3. **Feedback items.** Partner badges (with a fairness guard), learner badges, listing checks for Event and Job
   opportunities, and a comparison of Zeki against Yoma's existing review.

## Shared contract

`src/web/src/api/models/pathway.ts` is the source of truth (endpoints listed at the top). The API mirrors it in
`Yoma.Core.Domain.Pathway`. Change it only through the lead.

## Teams

| Team | Owns | Must not touch |
| ---- | ---- | -------------- |
| 1: My Yoma page | `src/web`: tab, onboarding, My Yoma page, badges UI, partner badge on the partner page | `src/api` |
| 2: Career mapping and outcomes | `Yoma.Core.Domain/Pathway` (engine, goals config, profile, outcomes), `PathwayController`, the **only** new migration, `Outcomes` grading factor, tests | badge logic, `src/web` |
| 3: Feedback items | `Yoma.Core.Domain/Pathway/Badges` (learner + partner badges), Event/Job listing checks in `CourseQuality`, comparison harness and doc, tests | the Pathway engine, migrations, `src/web` |

Rules: each team works in its own worktree; no team runs `scripts/course-quality-demo/start.sh` (the lead runs
the integrated demo); Team 3 needs no new tables (badges are computed); Team 2 is the only team adding a migration.

## Decisions

- Goals and their stages are configuration, like the rubric, so the team can change them without a release.
- Badges are computed from YoID and grades, not stored, so they never disagree with the data behind them.
- Outcome figures are hidden below a minimum group size (20 youth) and never shown as "Nx more likely".
- Drop-off flags a course for review; it does not lower the grade.
