# Course Quality Judger: iteration 2

Status: in-progress

Builds on the MVP in `README.md`. Same branch: `feature/course-quality-judger`.

## Goals

Set by the project lead on 2026-11-16:

1. **Course grades.** Say how good a course is, separately from Verified. Only the top 15–20% earn the
   top grade. Factors include provider reliability, length, comprehensiveness, a readable syllabus, and
   learner feedback. The rubric must be easy to change; the defaults are a placeholder.
2. **One journey across the three sides.** A seamless visual move between the learner, reviewer and
   partner views, and a click-through reviewer mode that shows one course at a time and why it was unsure.
3. **"Did you take it?" feedback.** When a signed-in learner leaves Yoma for a course and comes back,
   ask whether they took it. If yes, a short survey. Answers feed the grade.
4. **Every course.** The demo holds every live Yoma Learning listing (2,401 on 2026-11-16), each assessed
   and graded, not the 58 samples.

The learner card itself changes only by a small grade chip.

## Default rubric

From a research brief (QM CPE, SUNY OSCQR, ECBCheck, OpenupEd, UNESCO/COL, MOOC completion studies,
IMDb-style weighted ratings). Every number is configuration (`CourseQuality:Grading`) and can be
overridden from the rubric page. Present it as a "listing and learner-evidence grade", not a
certification of course content.

| Factor | Weight | Scored from | No data |
| ------ | ------ | ----------- | ------- |
| LearnerFeedback | 20 | Survey: per answer 0.5·(rating−1)/4 + 0.5·recommend; Bayesian mean with m = 10 towards the provider's mean | Neutral |
| SyllabusAvailable | 18 | Page: Course JSON-LD (+15), its syllabus/teaches/credential fields (+15), a "what you will learn / outcomes" list of 3+ (+30), a curriculum/modules/lessons list of 3+ (+30), prerequisites or audience (+10). Listing text only: same checks × 0.5 | Listing text |
| ProviderReliability | 15 | Smoothed check pass rate (pass + 8)/(n + 10) (40%), dead links per listing (20%), pooled learner score (33.33%), listing count capped at 20 (6.67%). Every graded course's organisation is approved, so that is not a signal | Prior 0.8 pass rate |
| Comprehensiveness | 12 | Module count (log, cap 15), assessment mentioned, certificate mentioned, skills (cap 5), description length (cap ~1,500 chars) | Neutral |
| CourseLength | 10 | Hours: under 0.5 → 40; 0.5–2 → 40 to 100; 2–20 → 100; 20–60 → 100 to 60; 60–100 → 60 to 40; over 100 → 40 | Neutral |
| Completion | 10 | (completed + α)/(started + 20), prior α/20 at the catalogue median, scored as a percentile within its length bucket | Neutral |
| ListingIntegrity | 8 | Verification result and confidence, link failures, listing more than 3 years old | 50 |
| Accessibility | 7 | Free 40 / free with paid certificate 25 / paid 0 / unknown 20; non-English or 2+ languages +30; mobile, offline or text +30 | Unknown cost 20 |

Grades are bands of the weighted score's percentile among graded courses:

| Grade | Label | Share | Also needs |
| ----- | ----- | ----- | ---------- |
| A | Top pick | Best 17% | Verified, score ≥ 65, syllabus ≥ 50, feedback ≥ 60 once it has 5+ answers. Otherwise B |
| B | Strong | Next 23% | |
| C | Good | Next 35% | |
| D | Basic | Last 25% | |

- **NotVerified courses are not graded** and are left out of the percentiles. **NeedsReview courses are not
  graded by default** either (`NeedsReviewMaxGrade` = `Ungraded`, reason "Not graded until a reviewer confirms
  it"): on the full catalogue 334 of 336 non-Alison courses were NeedsReview with unreadable pages, so they
  lacked syllabus and cost evidence and all landed in D, grading missing evidence rather than quality. Once a
  reviewer verifies one, the next regrade grades it. Setting `NeedsReviewMaxGrade` to a grade caps them there instead.
- A course needs data on at least 3 factors to be graded.
- Ties break on feedback, then syllabus, then completion, then length. A tied group on a cut-off goes to
  the lower band, so A stays at or under its share. A's cut-off is 17%, not 15%: on the real catalogue large
  tied groups sit at the cut-off, so 15% gave 8.6% A and 17% gives 16.1%, inside the 15–20% target.
- Not scored, on purpose: views, Zlto reward, difficulty, number of countries or tags, free-text
  sentiment.
- The whole catalogue is graded together because grades are relative. A change (an assessment, a reviewer
  decision, a survey answer) requests one regrade `RegradeDelayInSeconds` (30) later, shared by every change
  until it runs; an admin can also run it at once. Rubric version `g2-verified-only`.

## Decisions

- **Grades are separate from Verified.** Verified stays a fact about the listing. The grade is a
  judgement about the course and only ever ranks within what is graded.
- **The rubric is configuration with an admin override.** Defaults come from appsettings. The rubric page
  re-weights the catalogue in the browser from stored factor scores, so changes preview instantly. Saving
  stores an override in the database and regrades; reset returns to configuration.
- **Factor scores are absolute (0–100) and explainable;** only the final bands are percentiles. A
  reviewer can read why a course scored what it did.
- **Browse order becomes verified, then grade, then popularity.** It replaces verified-then-popularity
  when `ShowInBrowse` is on. An explicit sort and the newest-first New section are kept.
- **Feedback is signed-in only and opt-out.** A click on the course link while signed in records that
  the learner left. On a later visit (after `PromptDelayInMinutes`, one day by default and one minute in
  the demo) Yoma asks once. "Not yet" asks again after `SnoozeInDays`; closing the prompt never asks
  again about that course.
- **Comments are for Yoma only.** Partners and the public see ratings and tag counts, never comment text,
  because comments may come from children. Comments are capped at 500 characters.
- **Seamless views in the demo use one account.** The demo admin is also a partner admin, so switching
  between learner, reviewer and partner is navigation, not a new sign-in. The switcher is demo-only,
  behind `NEXT_PUBLIC_COURSE_QUALITY_DEMO`.
- **The full catalogue is assessed once with live pages, politely.** Pages are fetched at the product's
  normal pace (1.1 s per host) once; the results are saved to a local cache so later demo starts load
  them in seconds. The cache is git-ignored: it is public Yoma data, but large.
- **Feedback API rules (F1).** `left` and `pending` are no-ops (204 / `[]`) when feedback is off; submit is a 400 then; options and
  summary always work. Missing course 404; not a publicly visible Learning course 400. Survey fields are accepted only with Taken
  (else 400); Pending is not an answer. Taken may be resubmitted to update the survey but not changed to another answer;
  NotTaken and Dismissed are final (400). A submit with no prior `left` creates the row. Tags must be configured keys (case-
  insensitive, de-duplicated, stored in configured spelling); comment trimmed, blank is null, over the cap 400.
- **Every change requests one delayed regrade.** A regrade reads the whole catalogue (about 0.2 s in memory for 2,400 courses,
  plus the database read) under the grading lock, so assessments, reviewer decisions and Taken answers never regrade inline:
  they request a single Hangfire job `RegradeDelayInSeconds` later, shared by every change until it runs, and independent of
  `Enabled` and `BatchSize`. The hourly batch also requests one when a Taken answer is newer than its course's grade, as a safety
  net. `grading/run` and rubric saves regrade at once. Found by the independent review: regrading inside each assessment made a
  full-catalogue run quadratic.
- **A rubric is dry-run before it is saved.** Parameters have bounds, all four bands A–D are required, and the engine grades the
  current catalogue with the new rubric first; a failure is a 400 and nothing is saved, so a saved rubric cannot break grading.
- **The A feedback floor counts scored answers only.** `FeedbackResponses` means learners who said they took the course and gave
  a rating or a recommendation. The summary's `Responses` still counts everyone who said they took it.
- **AdjustedRating is the LearnerFeedback factor score on the 1–5 scale:** 1 + score / 25, so score = 25 × (AdjustedRating − 1)
  exactly, with the provider prior read from the same population as a grading run. It blends rating and recommendation, as the factor does.
- **Summary access:** Admin, or OrganizationAdmin of the course's organisation (another organisation gets the API's usual 401).
  Comments only for Admin, newest first, at most 10; tags list only those picked. Assessment `feedback` is null when nobody said
  they took the course.

## Contract

Source of truth, changed only by the orchestrator:

- `src/api/src/domain/Yoma.Core.Domain/CourseQuality/Models/CourseQualityGradeContract.cs` (new)
- `src/api/src/domain/Yoma.Core.Domain/CourseQuality/Models/CourseQualityContract.cs` (adds `LearnerCard.Grade`,
  `GradeLabel`; `CourseQualityAssessment.ReviewReasons`, `Grade`, `Feedback`; filter `Grade`)
- `src/web/src/api/models/courseQuality.ts`

### New endpoints (base `/api/v3/courseQuality`)

| Method | Route | Access | Returns |
| ------ | ----- | ------ | ------- |
| GET | `grading/rubric` | Admin | `CourseGradeRubricState` |
| PUT | `grading/rubric` | Admin | `CourseGradeRubricState`, after saving the override and regrading |
| DELETE | `grading/rubric` | Admin | `CourseGradeRubricState`, back to configuration and regraded |
| GET | `grading/factors` | Admin | `CourseGradeFactorRow[]`, every graded or gradeable course |
| POST | `grading/run` | Admin | `CourseGradeRubricState`, after regrading now |
| GET | `opportunity/{opportunityId}/grade` | Admin | `CourseGradeResult`, 404 when none |
| GET | `feedback/options` | Anonymous | `CourseFeedbackOptions` |
| POST | `opportunity/{opportunityId}/feedback/left` | Signed in | 204. Records that the learner left for the course |
| GET | `feedback/pending` | Signed in | `CourseFeedbackPrompt[]`, at most one, due now |
| POST | `opportunity/{opportunityId}/feedback` | Signed in | 204 |
| GET | `opportunity/{opportunityId}/feedback/summary` | Admin, or OrganizationAdmin of its organisation | `CourseFeedbackSummary` (comments empty unless Admin) |

`review/search` items now carry `reviewReasons`, `grade` and `feedback`. Search results and the public
card carry `grade` and `gradeLabel` on the learner card.

### Storage

One migration adds:

- `CourseQuality.Grade`: one row per opportunity, upserted per run: grade, score, percentile, factors and
  caps (JSON), rubric version, date graded.
- `CourseQuality.GradeRubric`: the admin override, at most one current row, with who saved it and when.
- `CourseQuality.LearnerFeedback`: one row per user and course: answer, date left, date asked, snooze
  until, rating, completed, would recommend, matched description, good and bad tags (JSON), comment.
- `CourseQuality.Assessment`: new nullable `PageEvidence` (JSON: syllabus, outcomes and module counts,
  certificate, assessment, languages, word count) and `ReviewReasons` (JSON).

## Teams

| Team | Member | Owns | Must not touch |
| ---- | ------ | ---- | -------------- |
| API | G1 Grading | Page evidence, grading engine, rubric store, migration (all three tables), grading endpoints, browse order, review reasons, tests | `src/web`, demo scripts |
| API | F1 Feedback | Feedback service and endpoints, feedback summary, LearnerFeedback factor input, tests. Starts after G1 (shared build output) | `src/web`, migration (G1 owns it) |
| Web | W1 Reviewer | Click-through reviewer mode, review reasons, grade breakdown, rubric page, demo view switcher, grade chip | Learner feedback prompt |
| Web | W2 Learner | Return prompt, survey, recording "left", feedback shown to reviewers and partners | Reviewer mode, rubric page |
| Data | D1 Catalogue | Live catalogue import, page snapshot, full assessment and grading run, demo seed of feedback and participation, start/stop scripts | `src/` |

The orchestrator owns this file, the contract files, integration, the full-catalogue run and the handoff.

## Tasks

- [x] Research and default rubric
- [x] Contract and plan
- [x] G1: page evidence, grading, rubric store, migration, endpoints, browse order, review reasons
- [x] F1: learner feedback API
- [x] W1: reviewer mode, rubric page, view switcher, grade chip
- [x] W2: return prompt and survey, feedback in reviewer and partner views
- [x] D1: all 2,401 courses in the demo, assessed and graded
- [x] Calibrate: A share 15–20%, spread within Alison, A share by provider
- [x] End-to-end browser check of all three sides and the feedback loop
- [x] Independent review, handoff
- [ ] Partner view of learner feedback on verified courses (today it shows only on held-back courses)
- [ ] Spread within Alison: most Alison scores sit between 71 and 74, so one point moves a band; consider decimal scores or rank-normalised factors
- [ ] Hand-rate about 100 courses and compare with the grades before launch (research brief's validation step)
