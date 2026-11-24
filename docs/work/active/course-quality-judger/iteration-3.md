# Course Quality Judger ("Zeki"): iteration 3

Status: planning

Builds on `iteration-2.md`. Same branch: `feature/course-quality-judger`, from `5667e2f`.
The product is now presented as **Zeki**, the course quality auditor.

## Feedback received (demo, before 2026-11-24)

| # | From | Feedback |
| - | ---- | -------- |
| F1 | Camila | Stronger than Yoma's existing reviewing system. |
| F2 | Camila | Partner badges, so providers such as Alison have a reason to publish better courses. |
| F3 | Camila | Grades should reflect outcomes after completion: employment, or moving on to an intermediate course. Yoma is starting to record employment as verifiable credentials on the youth identity. |
| F4 | Gloria | A pathway view: course X → course Y → job. A large drop-off at one step points to a problem with that course. |
| F5 | Demo chat | Cover the other opportunity types, not only Learning. |

## Position on each item

### F1: "better than the current system" is a claim, not yet evidence

Nobody has compared the two. Before this is repeated to anyone outside the team:

- [ ] Confirm with Camila what the existing reviewing system is (opportunity approval by admins,
      completion verification, or something else) and what it outputs.
- [ ] Build the hand-rated sample that iteration 2 already lists as missing: 100 courses, stratified by
      provider and grade, rated by two people blind to Zeki's grade. Report agreement (Cohen's kappa)
      for Verified and grade band, and the same for the existing system where it produces a verdict.
- [ ] Only then state "better", with the numbers.

### F2: partner badges, doable now, but not fair yet

Grades already exist per course, so a provider-level badge is an aggregate. Proposed defaults
(configuration, like the rubric):

- Eligible: at least 10 graded courses.
- Badge levels from the share of graded courses at A or B, and the share held back as Not verified.
- Recomputed with the regrade job; shown on the partner page and as a small mark on the learner card.

Risk: non-Alison courses are nearly all waiting for review (iteration 2, Known gaps), so today only
Alison could earn a badge. A badge launched now rewards the provider our pipeline can read, not the
best provider. Clear the review backlog, or show "not enough graded courses" rather than no badge.

### F3: outcome factor, build the slot, not the claim

- There is no outcome data yet. A factor fed by synthetic data must not appear in a demo as if real.
- "3x more likely to get a job" is a causal claim. Completers differ from non-completers (motivation,
  location, prior education), so the raw ratio measures selection as much as the course. Show
  "N% of completers later verified a job within 6 months (n = …)" next to the provider and catalogue
  baselines, and never a multiplier unless the comparison group is matched.
- Plan: add `Outcomes` to the rubric with weight 0 and "no data → neutral", a minimum sample
  (e.g. 30 completers) before it counts, and k-anonymity (no figure from fewer than 20 youth).
- Signals, in order of availability: progression to another Learning completion (data exists now,
  from completions); a Job opportunity completion; an employment credential on the youth identity.
- [ ] Confirm with Adrian where employment credentials land in the schema and the consent terms
      for using them in aggregate.

### F4: pathway view, mine first, curate second

- Completion records with timestamps already give transitions per youth: Learning → Learning → Job.
  Build the transition graph from those, keep edges with enough youth, and flag a step whose
  continuation rate is well below its siblings'.
- A drop-off is a signal for review, not a grade penalty: youth stop for reasons that are not the
  course (they got a job, the next course is paid, it is in another language).
- Curated pathways (Gloria's X → Y → job) come after the mined view shows which ones exist.
- This also feeds F3: progression is the first outcome signal we can measure today.

### F5: other opportunity types, there are five, not three

`Opportunity.Type` has Learning, Event, Job, ImpactAction, Entrepreneurship and Other. The listing
checks (live, link works, real dates, enough data) apply to all of them; the grade factors are about
courses and do not.

- Step 1: run verification (not grading) on Event and Job. Expired events and dead job links are the
  same hygiene problem the MVP fixed for courses.
- Step 2: a grade only where "good" can be defined. Job is the strongest candidate (salary shown,
  employer verified, still open); ImpactAction, Entrepreneurship and Other need a product decision.

## Proposed order

1. F2 badges, with the fairness guard (uses data that exists).
2. F5 step 1, verification for Event and Job (reuses the MVP checks).
3. F4 mined pathways and drop-off flags (data exists).
4. F3 outcome factor slot at weight 0, fed first by progression from F4.
5. F1 validation sample, in parallel, since it needs people rather than code.

## Open questions

- What is the existing reviewing system Camila compared Zeki with?
- Do badges need partner consent or sign-off before they are public?
- Which opportunity types matter most to the team after Learning?
- Linear ticket(s) for this work, so the folder can follow the `<TICKET>-<slug>` convention.
