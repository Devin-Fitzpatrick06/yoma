# Zeki against Yoma's existing review (F1)

Status: in-progress. Code comparison done; the comparison script is built and tested on fixture data only; the hand-rated
sample has not been run.

Camila said Zeki is "stronger than Yoma's existing reviewing system" (iteration 3, F1). This page records what the existing
review is, according to the code, what each side catches, and how to measure the difference. It does not claim that Zeki is
better: the only measured figures below are counts of listings the two treat differently, which say they disagree, not which
is right. That needs the hand-rated sample at the end.

Open question still to confirm with Camila: which review she meant. The code has no review of a listing's content, so she may
mean organisation approval, completion verification, or a manual process outside the code.

## What Yoma reviews today (from the code)

Paths are under `src/api/src/domain/Yoma.Core.Domain/`.

| Step | What it checks | Where |
| ---- | -------------- | ----- |
| Organisation approval | A new organisation starts Inactive; an admin approves (Active) or declines it. One left Inactive is auto-declined after `OrganizationDeclinationIntervalInDays`. A declined organisation that edits its details goes back for approval. This judges the **partner**, not its listings. | `Entity/Services/OrganizationService.cs` (`Create` sets Inactive, `UpdateStatus`, `SendForReapproval`); `Entity/Services/OrganizationBackgroundService.cs` (`ProcessDeclination`) |
| Publishing a listing | No review. An Active organisation's admin posts a listing as Active (`PostAsActive`) and it is public at once. The only gate is that the organisation is Active. | `Opportunity/Services/OpportunityService.cs` (`Create`: status from `PostAsActive`, "organization is not currently active") |
| Form validation | Title and description present; URL a valid address **if given** (it may be left out); end date not before start; a Job needs an application deadline (manual entry only); commitment required except for Job and Entrepreneurship. Nothing checks that the link opens, the dates are real or the page matches. | `Opportunity/Validators/OpportunityRequestValidatorBase.cs` |
| Expiry | A background job sets Active listings whose **end date** has passed to Expired, and later deletes Expired ones. A listing with no end date never expires. | `Opportunity/Services/OpportunityBackgroundService.cs` (`ProcessExpiration`, `ProcessDeletion`) |
| Public visibility | Search shows Active and NotStarted listings of Active organisations that are not hidden. Admins can hide or feature a listing. | `Opportunity/Services/OpportunityService.Search.cs` (`ApplyPublishedStates`), `OpportunityService.UpdateHidden`, `UpdateFeatured` |
| Partner sync | Listings pushed to a partner (SAYouth) only while Active and the organisation Active; CSV-imported listings are posted Active unless already ended. No content check. | `PartnerSync/Services/ProcessingService.cs` (`Statuses_Opportunity_Creatable`), `PartnerSync/Services/SyncBackgroundService.cs`, `OpportunityService.ImportFromCSV` |
| Completion verification | Reviews the **youth**, not the listing: an organisation admin approves or declines a learner's proof, or a completion arrives by action link, CSV import or partner sync. | `MyOpportunity/Services/MyOpportunityService.cs` (`FinalizeVerificationManual`, `PerformActionInstantVerification`, `PerformActionImportVerificationFromCSV`, `PerformActionImportVerificationFromPartnerSync`) |

In short, the existing review trusts an approved partner's listings and removes them by end date. There is no check of the
link, the page, the format or the content, and no quality ranking.

## What Zeki reviews

Per listing (`CourseQuality/Services/CourseQualityRules.cs`, `CourseQualityEngine.cs`, `CourseQualitySourcePageReader.cs`): the
listing is live, the link opens a usable page, the dates are real and (where the page shows them) match, the learner card has
what it needs, and the listing is a course or a webinar. Unsure results go to a reviewer, never to a pass. Verified courses are
then graded A to D on eight factors (`CourseQualityGradingEngine.cs`). Since this iteration the listing checks also run on Event
and Job when configured (`CourseQuality:VerifyTypes`).

Zeki does not judge the partner organisation and does not verify learners' completions; those stay with the existing review.

## Side by side on the README's failure types

| Failure type | Existing review | Zeki | Caveat |
| ------------ | --------------- | ---- | ------ |
| Expired listings | Caught when the listing has an end date, on the expiry job's next run. A listing with no end date is never expired: **2,211 of 2,411** live Learning listings (92%) have none (public catalogue, 24 Nov 2026). | Same end-date rule, plus an ended webinar with no end date, plus a course page that says it is closed or gone. | The page reading is wording-based and can miss or misread closure notices; misreads go to review. |
| Broken links | Not checked. A listing may have no link at all: **15 of 2,411** live listings have none. | Checked on every assessment: missing, invalid, private address, 404, unknown domain fail; timeouts and bot walls are "could not confirm" and go to review. | A site that blocks bots looks unconfirmed, not broken; that is most non-Alison courses (iteration 2, Known gaps). |
| Webinars listed as courses | Not checked; Yoma has no format field for Learning. **25** live titles say webinar, masterclass, workshop or live session. | Every listing is designated Course or Webinar, from the page, the listing wording or a reviewer. | The designation is a label, not a hold-back on its own; it holds back only an unknown format (to review) or an ended webinar. |
| Title/content mismatch | Not checked. | The page must match the listing title; a weak or no match goes to review, never to Verified. | Matching is lexical; a renamed course with an unchanged page can be sent to review. |
| Quality | Not judged. Listings are ranked by search relevance or date. | Verified courses graded A to D, relative to the rest of the catalogue. | The grade is a listing and learner-evidence grade, not a certificate of content. It has not been checked against human judgement. |

The 15, 2,211 and 25 counts come from the public signed-out catalogue saved by `seed/fetch-live-catalogue.mjs` on 24 Nov 2026
(2,411 listings, publishedStates NotStarted and Active). They describe what the existing review lets through. They do not
show that any of these listings is wrong.

## Where the two disagree on the demo database

`scripts/course-quality-demo/compare-review.sh` (with `compare-review.sql`) lists every listing the existing review approves
(publicly visible Learning: Active, organisation Active, not hidden) that Zeki holds back (latest assessment Not verified or
waiting for review), with counts by failure type, the webinars, grades, a per-organisation table and the course list.

```bash
scripts/course-quality-demo/compare-review.sh                   # against the running demo database
scripts/course-quality-demo/compare-review.sh --list 200        # longer course list
scripts/course-quality-demo/compare-review.sh --csv > held.csv  # every held-back course
scripts/course-quality-demo/compare-review.sh --database-url postgresql://...  # another copy of the schema, local psql
```

It is read-only (one temporary view). The "also caught by the expiry job" column separates listings the existing review would
remove on its own (end date passed) from those only Zeki catches.

**Not yet run on the demo database:** this session had no Docker, so the script was tested only on a fixture schema with the
real table and column names (every row type of the output checked by hand). The figures from the last full demo run, reported
in the 2026-11-16 handoff and not re-measured here, were: 2,409 assessed, 378 not graded, of which 358 waiting for review and
20 Not verified. Read as "approved by the existing review but held back by Zeki", that is 378 of about 2,400 (16%), and most
of it is "could not read the page" rather than a defect found. Run the script on the next demo start for the breakdown.

## How to measure "better": the hand-rated sample

Agreement with a person, not disagreement between the two systems, is the measure.

1. **Sample.** 100 live Learning listings, stratified by provider (Alison and the rest in proportion, but at least 30 non-Alison)
   and by Zeki result (Verified, waiting for review, Not verified, and grades A to D). Fix the random seed and record the ids.
2. **Raters.** Two people, blind to Zeki's result and to each other. Each opens the listing and its page and records: live (yes /
   no / cannot tell), link works, dates match, course or webinar, page matches the listing, and a quality band (top, strong,
   good, basic) using a one-page guide written before rating starts.
3. **Agreement between raters.** Cohen's kappa per question. Below 0.6, refine the guide and re-rate a fresh sample before
   comparing anything with Zeki.
4. **Zeki against the raters.** On listings where the raters agree: kappa for Verified against "live, link works, dates match and
   page matches", and weighted kappa for the grade band. Report the confusion table, especially Verified listings the raters fail.
5. **The existing review against the raters.** It approves every one of the 100 (they are live), so its agreement is the share of
   listings the raters pass. That figure, next to Zeki's kappa, is the comparison.
6. **Report** n, kappa with a 95% interval (bootstrap), and the disagreements by failure type. Say "better" only where the
   interval for Zeki is clearly above the existing review's agreement.

Cost: about 100 × 3 minutes per rater, roughly 5 hours each.

## Decisions

- "Approved by the existing review" means publicly visible, because the code has no listing review beyond organisation approval
  and expiry. If Camila meant a manual process, add it here and to the script.
- The script counts Zeki's "waiting for review" as held back, because a learner does not see Verified on it; the output always
  splits it from Not verified, since waiting is not a finding against the listing.
