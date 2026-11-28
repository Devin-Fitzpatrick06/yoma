# Known limitations and suggested next steps

## Known limitations

### Evidence and data

1. **Not validated against people yet.** No hand-rated sample has been scored, so Ziqi's verdicts and grades have not
   been compared with human judgement, and "better than the existing review" is not yet shown. The tools are ready
   (`test-cases/README.md`, "Validation sample").
2. **Grades are relative.** Bands are percentiles across graded courses. Alison is about 85% of the catalogue, so most
   comparisons are Alison against Alison; its scores cluster (most 71–74), so one point can move a band. Partner
   badges inherit this: a 75% A-or-B bar for Platinum is hard to reach by construction.
3. **Many non-Alison pages cannot be read** (sign-in walls, bot protection, app shells). Those courses wait for a
   reviewer rather than being graded, so smaller partners are under-graded until reviewers clear the queue.
4. **No real outcome or drop-off data.** Outcomes in the demo come from synthetic learners; employment shows
   "unknown" while Yoma has no job completions; drop-off is reserved ("Collecting from the pilot"). The Outcomes and
   DropOff grade factors are at weight 0.
5. **Synthetic learner feedback is not labelled in the reviewer interface** (it is in the demo data and docs).
6. **Job checks read the listing and the partner feed, not the job page** (JobJack pages are an app shell, Jobberman
   pages always contain "no longer available" text). Pay and experience come from listing text in the demo, because
   the public API does not return the feeds' custom fields.
7. **Events and impact actions get listing checks only, no grade.** Entrepreneurship and Other opportunities are not
   checked.

### Product and engineering

8. **AI is optional and off by default.** Without a key the keyless reader caps confidence, so more courses go to a
   reviewer. "Draft this for me" is not implemented (no `ICourseQualityDraftSuggester`). There is no dedicated
   prompt-injection test; the defences are untrusted-data delimiters and the rule that the AI can only lower a result.
9. **Pathways are rules and configuration.** Six career goals with matching patterns; some stage picks are weak (for
   example a creative-writing course under "Get confident online"). Goals, stages and patterns need tuning with the
   Yoma team. Yoma's own goal ("Get a job", "Learn new skills", …) is broader than a career, so the career question is
   still asked.
10. **"Usually go on to" appears rarely**: only where at least 5 youth took the same next step after at least 20
   completions (privacy floors).
11. **Swaps**: after a reload a swapped step can move one position (it stays on the path); swap history is never
   collapsed.
12. **Path building is per request** (with short caches). Fine for a demo; measure before a large pilot.
13. **The draft check's link cache is in memory per API instance**; several instances each keep their own.
14. **With the pre-publish switch off, a signed-out caller gets 401 rather than 404** from the draft check (the
   pathway switch returns 404 before sign-in).
15. **ZLTO balance is not shown** on the My Yoma YoID card.
16. **Not reviewed by Yoma's owners.** API changes need Adrian's review and web changes Jason's; visible changes need
   Jason's design approval before they leave the demo. Nothing is merged to `master` or deployed.
17. **Not run in production conditions**: no load test; migrations applied to a local database only; production-size
   data, multiple instances and Hangfire scheduling at scale are untested.

## Suggested next steps (in order)

1. **Run the demo with real grades** (`start.sh --full` on a machine with internet) and walk the script in
   `docs/work/active/course-quality-judger/demo-walkthrough.md`.
2. **Validate**: draw the 100-course sample, have two people rate it blind, score it (`test-cases/README.md`). Only
   then claim "better than the existing review".
3. **Decide the open settings with the project lead**: Platinum thresholds and badge cadence; whether the partner
   panel should hide for types Ziqi does not check; goal and stage patterns for pathways.
4. **Owner reviews and design approval**: Adrian (API), Jason (web, and the design of My Yoma, the journey map and the
   partner panel). Then squash-merge to `master` behind the switches (all off).
5. **Pilot with real youth** with `Pathway:Enabled` on for a small group: drop-off and outcome data start
   accumulating; turn on `Pathway:DropOffAvailable` and raise the Outcomes and DropOff weights only once groups pass
   the minimum sizes.
6. **Turn on the AI** with a key held in the deployment's secret store; implement "Draft this for me" as an
   `ICourseQualityDraftSuggester` in the Anthropic project.
7. **Clear the review queue** for non-Alison partners so smaller providers get graded and can earn badges.
8. **Connect the other BBS concepts** (PathFinder check-ins via RapidPro, YoCV on the YoID public profile, SkillLens
   results as YoID credentials, QR employer confirmation) — the project lead's next instructions.
9. **Adopt Yoma's v5 credential schemas** when they arrive (`data-mapping.md`), including a pathway-milestone
   credential.
