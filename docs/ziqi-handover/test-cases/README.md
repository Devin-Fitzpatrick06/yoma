# Test cases

| File | What it is |
| --- | --- |
| `api-tests.md` | Every API test case for Ziqi (course quality, jobs, draft check, pathways, badges, handover), generated from the test runner's discovery. |
| `web-tests.md` | The web unit-test cases for the Ziqi, My Yoma, badge and partner-panel code, listed by file. |

## Running them

See `../SETUP.md`, "Tests". In short, from `src/api`:

```bash
dotnet run --project src/test/Yoma.Core.Test -- -namespace "Yoma.Core.Test.CourseQuality*" -namespace "Yoma.Core.Test.Pathway*" -namespace "Yoma.Core.Test.Handover*"
```

and from `src/web`:

```bash
pnpm test
```

## What the main suites prove

- **Verification rules and routing** (`CourseQualityEngineTests`, `CourseQualityServiceTests`,
  `CourseQualityListingTypesTests`, `CourseQualityDatesAndFormatTests`, `CourseQualitySourcePageReaderTests`): every
  check, hard gate, threshold and the review routing, for courses, events and impact actions.
- **Draft check ≡ post-publish check** (`CourseQualityDraftCheckTests`): 72 equivalence cases (Learning, Event, Job,
  Impact Action × 9 scenarios, rules only and with the page reader) proving a partner sees the same verdict before
  publishing as after.
- **Grading** (`CourseQualityGradingEngineTests`, `CourseQualityJobGradingTests`): every factor, bands, ties, A floors,
  the rubric round-trip, and that Outcomes and DropOff are neutral at weight 0.
- **Link safety** (`CourseQualityLinkCheckerTests`): the link checker refuses private and local addresses and pins
  the address it resolved.
- **AI can only lower** (`CourseQualityAnthropicTests`): the model cannot raise confidence or confirm dates the fixed
  checks could not find, a "Free" it cannot back is Unknown, and no key means no client. There is no dedicated
  prompt-injection test yet; the defence is the untrusted-data delimiters plus this ceiling.
- **Jobs** (`CourseQualityJobTests`, `CourseQualityJobGradingTests`): job checks, scam signals, freshness, job grades.
- **Pathways** (`Pathway…Tests`): preferences answer questions and are never overwritten with placeholders,
  determinism, 8–12 steps, practise stages and the related-impact fallback, locks and swaps, alternatives,
  "usually go on to" privacy floors, outcomes never stating a cause, step events, drop-off staying "Collecting".
- **Badges** (`PathwayBadgeTests`, `PathwayPlatformTests`): Bronze → Platinum thresholds and hints, the fairness guard,
  review dates, the batched endpoint, anti-gaming rules for learner badges, and that every `/pathway` endpoint is off
  (404) unless switched on.
- **Handover** (`HandoverExportTests`): the files in `../prompts` and `../rubrics` match the code.

## Opt-in tests (skipped unless their variable is set)

| Variable | Runs |
| --- | --- |
| `COURSE_QUALITY_LIVE=1` | Link checks and page reading against real websites |
| `COURSE_QUALITY_TEST_DB=<connection string>` | Migrations, jsonb columns and SQL against a disposable PostGIS database (superuser) |
| `COURSE_QUALITY_JOB_SAMPLE=<file>` | Calibration summary over a saved sample of live jobs |

One test, `RealResolver_RefusesLocalTargets("http://localhost./")`, needs a resolver that answers for `localhost.`;
it fails in sandboxes without real DNS and passes on a normal machine.

## Demo test data

- `scripts/course-quality-demo/seed/demo-courses.json`: the 8 named walkthrough courses (101–108), each with the
  result it is expected to get; the demo's start summary checks them ("8 as expected").
- `scripts/course-quality-demo/fixtures/server.mjs`: local stand-in course pages for those courses.
- The live catalogue, jobs and impact actions are fetched fresh into `scripts/course-quality-demo/.cache/`
  (git-ignored).

## Validation sample (human ground truth)

The protocol is in `docs/work/active/course-quality-judger/zeki-vs-current-review.md` ("How to measure better").
Tools in `scripts/course-quality-demo/validation/`:

1. With the demo running and courses graded (`start.sh --full`), draw a seeded, stratified sample of 100 courses:

   ```bash
   scripts/course-quality-demo/validation/sample.sh
   ```

   This writes `key.csv` (Ziqi's results, kept from the raters) and two rating sheets in different orders to
   `scripts/course-quality-demo/.cache/validation/`.
2. Two people rate their sheet blind (about 5 hours each).
3. Score:

   ```bash
   node scripts/course-quality-demo/validation/score.mjs
   ```

   It reports agreement between the raters (Cohen's kappa), Zeki's agreement with them, and whether the paired
   difference against the existing review is clearly above zero, the only condition under which "better" may be
   claimed. Tested on synthetic sheets only; no real rating has been done.
