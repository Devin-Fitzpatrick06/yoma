# Course Quality Judger (MVP)

Status: in-progress

No Linear ticket yet. Branch: `feature/course-quality-judger`, cut from `prototype`.

## Problem

New Yoma users (the cold start) cannot tell which courses are live, accurate or worth starting.
Tester feedback in the UNICEF decks names expired listings, broken links, webinars listed as
courses, and title/content mismatches. Quality varies by provider.

## Outcome

Every published `Learning` opportunity gets an assessment. It produces three outputs:

1. **Learner card** — public: verification state, timeline, upfront cost, shown before starting.
2. **Reviewer queue** — admin: low-confidence assessments for a human to confirm or override.
3. **Partner fix note** — organisation-facing: why a course is held back and what to fix.

## Verified means

The listing is live, its dates are accurate, its link works, and it has enough accurate data.

## MVP priorities

Set by the project lead. Scoring criteria are deferred to a later iteration, so the rubric below
is a placeholder and must not be tuned further for this build.

1. Every course has real dates.
2. Every listing is designated a course or a webinar.
3. Everything the learner card needs is in place.

## Team structure

| Team | Member | Owns | Must not touch |
| ---- | ------ | ---- | -------------- |
| B: grading | B1 Engine | `src/api` domain, rubric, link check, persistence, migration, controller, job, tests | `src/web`, the Anthropic project |
| B: grading | B2 AI | `Yoma.Core.Infrastructure.Anthropic` project and its tests | Domain logic beyond the `ICourseQualityAIProvider` contract |
| A: experience | A1 Learner | Learner card, opportunity page integration, preview fixtures | Admin and organisation pages |
| A: experience | A2 Operations | Reviewer queue page, partner fix note page | Opportunity page |

The orchestrator owns this file, the contract files and integration. B2 starts after B1 finishes,
because both run `dotnet build` on the same output folders. Team A runs alongside B1.

## Shared contract

Contract files are the source of truth. Change them only through the orchestrator:

- `src/api/src/domain/Yoma.Core.Domain/CourseQuality/Interfaces/ICourseQualityAIProvider.cs`
- `src/api/src/domain/Yoma.Core.Domain/CourseQuality/Models/CourseQualityContract.cs`
- `src/web/src/api/models/courseQuality.ts`

### Endpoints (base `/api/v3`)

| Method | Route | Access | Returns |
| ------ | ----- | ------ | ------- |
| GET | `/courseQuality/opportunity/{opportunityId}/card` | Anonymous | `LearnerCard`, 404 when none exists or the opportunity is not publicly visible |
| POST | `/courseQuality/opportunity/{opportunityId}/assess` | Admin | `CourseQualityAssessment` |
| POST | `/courseQuality/review/search` | Admin | `CourseQualityReviewSearchResults` |
| POST | `/courseQuality/review/{assessmentId}` | Admin | `CourseQualityAssessment` |
| GET | `/courseQuality/organization/{organizationId}/fixNotes` | Admin, OrganizationAdmin of that organisation | `PartnerFixNote[]` |
| GET | `/courseQuality/coverage` | Admin | `CourseQualityCoverage` |

### Rubric

The rubric is configuration, not code, so the final criteria can replace these defaults without a
rewrite. It lives in the `CourseQuality` appsettings section with `Version`, per-criterion
`Weight`, and thresholds.

| Criterion | Default weight | Hard gate | Check |
| --------- | -------------- | --------- | ----- |
| `ListingLive` | 20 | Yes | Active, published, not hidden, not expired |
| `LinkWorks` | 20 | Yes | Listing URL resolves to a usable page |
| `DatesAccurate` | 30 | Yes | Dates are real: present, coherent, not placeholders, and match the source page when AI is on |
| `DataComplete` | 15 | No | Every field the learner card needs is present and agrees with the source page |
| `FormatIdentified` | 15 | No | Listing is designated `Course` or `Webinar`; `Unknown` sends it to review |

### Routing

- `Verified`: score at or above `VerifiedScoreThreshold` (80), confidence at or above
  `ReviewConfidenceThreshold` (0.7), and no hard gate failed.
- `NeedsReview`: confidence below the threshold. Goes to the reviewer queue.
- `NotVerified`: confident, but a hard gate failed or the score is below the threshold. Creates a
  partner fix note.
- A failed hard gate caps the score at `HardGateScoreCap` (49).
- With no page evidence, confidence is capped at `RuleOnlyConfidenceCap` (0.69), so a listing
  whose page could not be read never produces `Verified`. It goes to review.
- The MVP runs with no API key. A built-in source-page reader implements
  `ICourseQualityAIProvider` with `IsAI = false`: it reads schema.org JSON-LD, meta tags and page
  text for dates, cost and format, and quotes its evidence. A listing can be `Verified` when the
  page confirms its dates and format. If a key is added later, the Anthropic provider takes over.
- A definite hard-gate failure from the rules, such as a 404 or an expired listing, is confident
  and goes to `NotVerified` with a fix note. Only inconclusive results go to review.
- A reviewer decision overrides the AI and is kept across reassessment until the listing's own
  content changes, tracked by a fingerprint of learner-relevant fields, not `DateModified`.
- A definite hard-gate failure ends a reviewer's `Verified`. The badge never outlives a dead link.
- Reviewer notes stay internal and are not shown to partners.

### Decisions

- **Courses means `Type.Learning`.** Other opportunity types are not assessed.
- **Upfront cost is never guessed.** Yoma has no cost field. Cost comes from AI extraction with an
  evidence quote, and otherwise shows as `Unknown`. A course is never called free without evidence.
- **Search ranking does not change in the MVP.** Courses show a verification state only.
- **Partner notes are not emailed in the MVP.** They appear in the product. Automatic outbound
  messages to partners need separate sign-off.
- **Learner-facing text stays factual.** A low score shows as "Not yet verified", never as an
  accusation against a partner.
- **Fetched pages are untrusted input.** The link checker blocks private and internal addresses,
  and limits redirects, response size and time. Page text is delimited as data in the AI prompt,
  and AI output is schema-validated and cannot override hard gates.
- **The AI provider is the official Anthropic C# SDK**, isolated in one infrastructure project,
  approved by the project lead. The model is configuration; the default is `claude-opus-5-5`.
- **The assessment job is off by default** (`CourseQuality:Enabled`), so nothing spends API credit
  or fetches external sites until someone turns it on.

## Real-listing validation

Rubric `v4-visible-proof`, from saved pages: a 58-listing sample across 29 providers gives 9
Verified, 46 NeedsReview and 3 NotVerified. A further 30 Alison listings give 30 Verified. 15 live
re-fetches of sampled Alison pages also gave Verified. Every badge is Alison, which is 2,044 of
2,393 Learning listings. No false badge was found. None of the readable pages confirmed a
listing's dates, so `LearnerCard.datesConfirmed` is false on all sampled listings.

Proof rules: a strong match needs a visible h1 equal to the listing title, structured data only
counts when visible page text agrees, and Claude can only lower the reader's result. Text hidden
by CSS is not detected.

## Open items

- [x] Self-paced courses: the project lead chose `Verified` with "Start anytime" in place of dates.
      A listing qualifies when the page says it is self-paced, or its verified organisation and
      the page's host both match `CourseQuality:SelfPacedProviders` (`Name` and `Domains`; Alison
      Learning on alison.com). It still needs a working link, a read page that strongly matches
      the listing, and a known format. The badge then means live and matching its page, not dates
      confirmed. The Anthropic provider applies the same fixed rules.

- [ ] Final scoring criteria and weights from the project lead, in a later iteration
- [ ] Anthropic API key for live AI runs
- [ ] Ownership handoff: this crosses Adrian's API and Jason's web areas
