# Rubrics and rule sets

Every rule that decides a Ziqi result is configuration with defaults in code. These JSON files are those defaults,
generated from the code and checked by a test (`src/api/src/test/Yoma.Core.Test/Handover/HandoverExportTests.cs`), so
they always match what runs. To change a rubric, change the setting in configuration (or the defaults class) — not
these files — then regenerate them.

| File | Configuration section | Defaults in code |
| --- | --- | --- |
| `verification-checks.json` | `CourseQuality` (without the grading rubrics) | `CourseQuality/Models/CourseQualityOptions.cs` |
| `course-grading-rubric.json` | `CourseQuality:Grading` (also editable on the admin rubric page) | `CourseQuality/Models/CourseQualityGradingDefaults.cs` |
| `job-grading-rubric.json` | `CourseQuality:Jobs:Grading` | `CourseQuality/Models/CourseQualityJobGradingDefaults.cs` |
| `partner-badges.json` | `Pathway:PartnerBadges` | `Pathway/Badges/Models/PathwayBadgeOptions.cs` |
| `learner-badges.json` | `Pathway:LearnerBadges` | `Pathway/Badges/Models/PathwayBadgeOptions.cs` |
| `pathway-goals-and-questions.json` | `Pathway` | `Pathway/Models/PathwayDefaults.cs`, `PathwayOptions.cs` |

Paths are under `src/api/src/domain/Yoma.Core.Domain/`.

## How to read them

**Verification** (`verification-checks.json`): each check (`Criteria`) has a `Weight` and `HardGate`. A listing is
Verified at `VerifiedScoreThreshold` (80) or more with every hard gate passed and confidence at least
`ReviewConfidenceThreshold` (0.7); a failed hard gate caps the score at `HardGateScoreCap` (49); a rules-only result
(no AI) is capped at `RuleOnlyConfidenceCap` (0.69), so it goes to a reviewer when the rules alone are not sure. Course
checks: ListingLive, LinkWorks, DatesAccurate, DataComplete, FormatIdentified. Job checks: JobOpen, NoScamSignals,
JobDetailsClear, JobFresh. `VerifyTypes` lists extra types that get the listing checks (Learning is always on).

**Course grades** (`course-grading-rubric.json`): `Factors` with `Weight`, `NeutralScore` (used when there is no
data) and `Parameters`; weights are normalised to 100. Bands (`Bands`) are percentiles among graded courses: A is the
top share, then B, C, D, with floors for A (for example a minimum number of feedback answers). Outcomes and DropOff
are present at weight 0 until real data exists. Only Verified courses are graded.

**Job grades** (`job-grading-rubric.json`): the same shape with job factors; jobs are ranked only against other jobs,
and A needs a verified job with a stated pay amount.

**Partner badges** (`partner-badges.json`): a partner needs `MinimumGradedCourses` graded courses (10). Each level
needs at least `MinShareTopGrades` of graded courses at A or B and at most `MaxShareHeldBack` held back as Not
verified; Platinum also needs 25 graded courses. Courses waiting for review count neither way. Badges are recomputed
from grades, never stored, and carry a review date (start of the next quarter). Thresholds are placeholders pending
the project lead's sign-off.

**Learner badges** (`learner-badges.json`): only the configurable number is here (`TopPickCompletions`: three
A-grade completions for "Top pick learner"). The seven badges and their anti-gaming rules are described in
`docs/work/active/course-quality-judger/badges.md`.

**Pathways** (`pathway-goals-and-questions.json`): `Goals` (six careers), each with `Stages` (name, why, matching
`Pattern` over skills and titles, preferred difficulties, step `Types` such as Learning or ImpactAction) and a
`JobsPattern` for the destination jobs; `UserGoals` link Yoma's own goals to careers. `Backgrounds`, `Hours`,
`KnownSkills` and `Countries` define the questions. Engine numbers (`MinCourses`, `MaxCourses`, stage sizes,
`MinimumGroupSize` for outcomes, `MaxAlternatives`) are at the top level.
