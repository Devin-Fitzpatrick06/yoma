# Badges (iteration 3, F2 and learner badges)

Status: in-progress — API built (Team 3); web (Team 1) and the pathway engine wiring (lead) pending.

Badges are computed on every request from YoID records and Zeki grades, never stored, so they cannot disagree with the data
behind them. Code: `src/api/src/domain/Yoma.Core.Domain/Pathway/Badges/`. Contract: `LearnerBadge` and `PartnerBadge` in
`src/web/src/api/models/pathway.ts`, mirrored in `Models/PathwayBadgeContract.cs`.

## Endpoints (base `/api/v3/pathway/badges`, `PathwayBadgeController`)

| Method | Route | Access | Returns |
| ------ | ----- | ------ | ------- |
| GET | `me` | User | `LearnerBadge[]`, all seven, earned or not, in a fixed order |
| GET | `organization/{organizationId}` | Anonymous | `PartnerBadge`; 404 for an organisation that does not exist or is not Active |

## Partner badge

Counted over the organisation's publicly visible Learning courses (Active, organisation Active, not hidden):

- **Graded**: stored grade A to D. **Top grades**: A or B. **Held back**: latest assessment Not verified (and no grade).
  **Waiting**: latest assessment needs review (and no grade).
- `shareTopGrades` = top / graded. `shareHeldBack` = held back / (graded + held back).
- **Fairness guard**: waiting courses are in neither share, and the hint says how many there are. Below
  `MinimumGradedCourses` (10) the level is `NotEnoughGraded` and both shares are null, so a provider is not labelled from a
  handful of courses.
- Levels (configuration `Pathway:PartnerBadges:Levels`, merged per level; placeholders):

| Level | Top grades at least | Held back at most |
| ----- | ------------------- | ----------------- |
| Gold | 60% | 5% |
| Silver | 45% | 10% |
| Bronze | 30% | 20% |

- `nextLevelHint` names the next level and what is short, in courses where it can ("about 2 more courses at grade A or B (20% now,
  30% needed)", "1 fewer course held back as Not verified"), then the waiting count. Gold has no next level.
- Event and Job assessments never count: only courses are graded.

**Caveat to show with any badge.** Grades are catalogue percentiles: A and B are about 40% of all graded courses. A provider
that is most of the catalogue (Alison: 2,061 of 2,411 live Learning listings on 24 Nov 2026) sits near 40% by construction, so
its level says little about it. And while non-Alison courses are mostly waiting for review, only Alison can reach the minimum.
Both are why the guard reports `NotEnoughGraded` rather than `None`.

Not done: the partner badge is not added to `organization/{organizationId}/fixNotes`, because that response is a bare array and
adding it would change the iteration 2 contract. The partner page can call the badge endpoint alongside it.

## Learner badges

Input: the learner's path (goal date and stages with course ids) and their completions. Completions are MyOpportunity rows with
action Verification, status Completed and a completion date. Each carries how it was recorded, the opportunity's skill count,
whether a credential was issued, and the course's current grade.

| Badge | Earned when | Date earned | Progress when not earned |
| ----- | ----------- | ----------- | ------------------------ |
| Pathfinder | A goal is set | Goal set (profile created) | 0 |
| FirstStep | Any course on the path is completed | Earliest path completion | 0 |
| StageComplete | Every course in a stage is completed (empty stages ignored) | Finish date of the first finished stage | Best stage's share |
| HalfwayThere | Half the path's distinct courses, rounded up | Date of that completion | done / needed |
| JobReady | Every course on the path | Last completion | done / total |
| VerifiedSkill | A **verified** completion of an opportunity that awards a skill (any type) | Earliest such | 0 |
| TopPickLearner | `TopPickCompletions` (3) Learning courses currently graded A | Date of the third | done / 3 |

Progress is rounded to 2 places and never reads 1 until earned.

### Anti-gaming rules

- **Self-reported never counts.** "Yes, I took it" in the course survey is not a completion. The adapter reads only completed
  verifications; the calculator also drops anything marked `SelfReported`, so a future caller cannot pass one through.
- **Evidence levels.** Verified: the organisation approved the learner's proof, imported the completion (CSV) or its platform
  synced it. ActionLink: instant verification through an organisation's link, which anyone holding the link can use. Action-link
  completions move a learner along the path but do **not** earn VerifiedSkill. An issued credential is not enough on its own,
  because credentials are issued for action-link completions too.
- **One opportunity counts once**, however many stages list it.
- **Declined, pending or deleted completions** earn nothing, and a badge disappears with its completion.
- **Dates come from the records**, never from when the badge was first shown. A partner's import or sync carries the partner's
  own completion date. A path badge can predate the goal when the work did.
- **TopPickLearner follows current grades**, so it can be lost if a course drops out of A. Grades need a Verified course with
  substance (syllabus and score floors), which limits farming short courses.
- **Switching goals** changes the path and so the path badges. Nothing is stored, so badges cannot be collected across goals.

Known weakness: action-link completions are recognised by the fixed comment `MyOpportunityService` writes ("Auto-verification:
Action Link"). Nothing else on the record marks them. If that wording changes, they would count as verified. A field on the
record would be sturdier (needs a migration, so not done here).

## Wiring for the pathway engine (lead)

- `IPathwayBadgeSource.ListForUser(userId, LearnerBadgePath?)` returns the badges for `MyYoma.badges`. Build
  `LearnerBadgePath(profile.DateCreated, stages.Select(s => new LearnerBadgeStage(s.Order, s.Name, s.Courses.Select(c => c.OpportunityId))))`.
  Use `Pathway.Badges.Models.LearnerBadge` for `MyYoma.badges` rather than a second copy of the model.
- Register an `IPathwayBadgePathProvider` so `GET pathway/badges/me` sees the path too. The default (`NoPathwayBadgePathProvider`)
  has none; then only VerifiedSkill and TopPickLearner can be earned.
- Defaults live in code. `Pathway:PartnerBadges` and `Pathway:LearnerBadges` are not in `appsettings.json`, to avoid colliding
  with Team 2's `Pathway:Goals`; add them when merging if the team wants them visible there.

## Contract change requested

`PartnerBadge` has no field for the number of courses waiting for review. It is in `nextLevelHint` text for now. Proposed:
`waitingForReview: number`.
