# Outcome data for Zeki: ways to measure learning → earning

Status: planning. Feedback F3 (Camila): course grades should reflect what happens after completion. This maps every
source this repo already holds or could hold, ranked by what it is worth. Paths are under `src/api/src/`.

## What is built (iteration 3)

- `Outcomes` grading factor, **weight 0** by default (`CourseQuality:Grading`), neutral without data, minimum group
  size 20 (`Pathway:MinimumGroupSize`).
- Per course (`Pathway/Services/PathwayOutcomeEngine.cs`): completers; **progression** (later completed another
  course towards the same goal); **employment** (later completed a Job opportunity on Yoma); **continuation** between
  path steps; a **drop-off flag** for review, never a grade penalty.
- Employment is `null` (unknown), not 0%, while Yoma has no job completions at all.
- Notes say "what happened next, not what the course caused". No multipliers ("3x") anywhere.

## The sources, best first

| # | Source | Where in the repo | Available today | Main risk | Use it for |
|---|--------|-------------------|-----------------|-----------|-----------|
| 1 | **Job credential on YoID.** The Job schema already carries `MyOpportunity_JobEmploymentStartDate`, employment type, salary range and pay interval | `domain/Yoma.Core.Domain/SSI/Services/SSIBackgroundService.cs` (Job schema seed), `MyOpportunity` verification | Schema yes; issued volume low | Only jobs listed **on Yoma** produce one: undercounts youth hired elsewhere | Employment rate; later salary band by course (aggregate only) |
| 2 | **Job opportunity completions** (verified) | `domain/Yoma.Core.Domain/MyOpportunity` (`VerificationStatus` Completed, `DateCompleted`) | Yes, wired as `employmentRate` | Same reach as 1; partner-synced jobs (Jobberman, JobJack) verify differently | Employment rate now |
| 3 | **Progression to an intermediate course** | `MyOpportunity` Learning completions + `Opportunity.Difficulty` + goal stages | Yes, wired as `progressionRate` | Alison's catalogue dominates; easy courses inflate it | Leading indicator while employment data is thin |
| 4 | **Path continuation and drop-off** | `PathwayOutcomeEngine.Continuation` | Yes | Youth stop for good reasons (got a job, paid next step) | Flag for review, Gloria's "where do they drop off" |
| 5 | **Follow-up survey at 3 and 6 months** ("Are you working? Did this course help?") | Extend `CourseQuality` learner feedback (`CourseQualityFeedbackService`, `LearnerFeedback` table) | Prompt mechanism yes; questions no | Self-report, low response, survivor bias | Employment outside Yoma; perceived usefulness |
| 6 | **Employer confirmation** from job partners (hired / interviewed) | `infrastructure/Yoma.Core.Infrastructure.Jobberman`, `.JobJack`, `.SAYouth` | No; needs partner agreements | Partner data quality; consent | Gold-standard hire signal for partner jobs |
| 7 | **Skill demand match**: course skills vs skills in live job ads (Emsi/Lightcast taxonomy) | `infrastructure/Yoma.Core.Infrastructure.Emsi`, `LaborMarketProvider` | Skills taxonomy yes; job ads carry few skills | Measures relevance, not outcomes | A "skills employers ask for" signal, never called an outcome |
| 8 | **Earning activity**: ZLTO earned and spent after completion, marketplace use | `domain/Yoma.Core.Domain/Reward`, `Marketplace` | Yes | ZLTO is a Yoma reward, not income | Engagement only; do not present as earnings |
| 9 | **Entrepreneurship completions** (started a business programme) | `Opportunity.Type.Entrepreneurship`, its credential schema | Yes | Programme completion ≠ business started | Secondary outcome for the business goal |
| 10 | **Referrals by completers** | `domain/Yoma.Core.Domain/Referral` | Yes | Social, not economic | Satisfaction proxy only |

## What this means for the grade

1. Keep `Outcomes` at weight 0 until a course has 20+ completers **and** Yoma has job outcome records in volume.
2. First weight it on progression (source 3), because it exists today, at a small weight (5) and only within the
   learner-evidence part of the grade.
3. Add employment (1 + 2) when at least a few hundred job credentials exist, compared against the **catalogue
   baseline** for the same goal, never as a ratio claim.
4. Survey answers (5) feed a separate, labelled line, never the grade, because they are self-reported.

## Why "3x more likely to get a job" stays off the screen

Completers differ from non-completers in motivation, location and prior education, so a raw ratio measures who
signs up as much as what the course does. Honest wording: "N% of youth who completed this course later started a job
listed on Yoma (n = …). Across the catalogue it is M%." A causal claim needs a comparison group (matched youth who
viewed but did not take the course) and is a research project, not a dashboard figure.

## Privacy and consent

- Aggregate only, minimum group size 20, never an individual's outcome shown to a partner.
- The employment credential belongs to the youth: confirm with Adrian that the YoID consent terms cover aggregate
  use for course grades before weighting source 1.
- Salary bands (source 1) only in aggregate and only once groups are large; they are the most sensitive field.

## Open questions

- Will Yoma issue a job credential for jobs found outside Yoma (self-declared, then verified)? That decides whether
  source 1 can ever reach most youth.
- Which partners would share hire confirmations (source 6)?
