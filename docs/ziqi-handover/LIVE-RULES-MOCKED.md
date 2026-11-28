# What is live, what is rule-based, what is mocked

Read this before showing anyone a number from the demo. Three kinds of logic run in Ziqi, and the demo adds a fourth
kind of data.

## Live (real data or real web requests)

| What | Detail |
| --- | --- |
| The catalogue | Every live Yoma course (about 2,400), up to 2,000 live jobs, the live impact actions (about 40) and the live events (17 on 28 November 2026), imported from the public Yoma search API (`api.yoma.world`) when the demo starts. A snapshot: it does not update while the demo runs. |
| Link checks | Ziqi opens each listing's link for real: redirects, status, final address, with a safe checker that refuses private and local addresses and pins the address it resolved. |
| Page reading | The course page that loads is read for real: dates, duration, cost, syllabus, structured data, webinar wording. Some providers block automated readers or show a sign-in shell; those pages go to a reviewer instead of being guessed. |
| The partner draft check | Runs the same checks on what the partner has typed, including a real (short-timeout) link check. Reads the page with the keyless reader, not the AI, unless `CourseQuality:PrePublish:UseAI` is on (off by default: a local CPU model is too slow for a form, and a draft check's AI usage is not recorded). |
| In production (not deployed) | Completions, YoID credentials and Yoma preferences would be real youth data. In the demo they belong to the demo accounts. |

## Rule-based (deterministic code and configuration)

Everything that decides a result is rules, not AI. The rules are in `rubrics/`.

| What | How |
| --- | --- |
| Verification ("Verified", "Needs a look", "Not verified") | Weighted checks with hard gates: listing live, link works, real dates, enough data, course or webinar format; jobs: open, no scam signals, clear details, freshness. Threshold 80; low confidence goes to a reviewer. `rubrics/verification-checks.json` |
| Course grades A–D | Eight factors (learner feedback, syllabus, provider reliability, comprehensiveness, length, completion, listing integrity, accessibility) plus Outcomes and DropOff at weight 0. Bands are percentiles across graded courses. `rubrics/course-grading-rubric.json` |
| Job grades A–D | Pay transparency, clarity, youth fit, listing safety, partner reliability, applicant feedback; ranked only against other jobs. `rubrics/job-grading-rubric.json` |
| Partner badges | Bronze → Platinum from the share of A/B grades and the share held back; at least 10 graded courses; courses waiting for review never count against a partner. `rubrics/partner-badges.json` |
| Learner badges | Seven badges from verified completions only (self-reported answers and one-click link completions never earn "Verified skill"). |
| Pathways (My Yoma) | Goals and stages are configuration: each stage matches courses or impact actions by skills and title patterns, level and length, then picks the best by Ziqi grade and completions. Locks and swaps are replayed. `rubrics/pathway-goals-and-questions.json` |
| Outcomes and "usually go on to" | Counted from completion records. Hidden below 20 completers, and a next step is named only when at least 5 youth took it. Never a cause or a multiplier. |
| Fix notes and the draft check's fixes | Generated from the failed checks, in partner wording. |

## AI (optional; in the demo a free local open model)

| What | Detail |
| --- | --- |
| Course page check | With an AI configured, Ziqi also asks the model to compare the listing with its page and to find the cost and format. The prompt and schema are in `prompts/`, the same for every provider. The AI can only **lower** a result: the fixed checks decide what the page proves, and a confident AI answer cannot verify a course the rules hold back. |
| In the demo: a local open model, free | `start.sh --local-ai` runs `qwen2.5:3b` in Ollama on the presenter's machine (OpenAI-compatible API). No data leaves the machine and nothing is billed: each run records cost $0.00. A reviewer runs it on demand (**Run AI check**); the start-up checks run without it. |
| Any OpenAI-compatible server | The same setting points Ziqi at vLLM, LM Studio or a government-hosted model (`--ai-url`, `--ai-model`). Cost is recorded only when a price is configured for that model, otherwise "unknown", never guessed. |
| Anthropic (optional, billed) | Still supported (`AI:Provider=Anthropic` with a key). It is billed per token by Anthropic. In this work it was tested with fakes only and never called for real. |
| Usage and cost per run | Every AI run stores provider, model, endpoint host, local or not, tokens in and out, cost, duration and outcome (answered, declined, failed, invalid) with the assessment. The reviewer card shows them; `GET /courseQuality/ai/usage` sums them. |
| Without an AI (the default) | The keyless page reader runs instead; results are capped below "confident" and more courses go to a reviewer. |
| "Draft this for me" (partner panel) | Not implemented yet: the panel shows "Available when AI is configured". The interface (`ICourseQualityDraftSuggester`) is in place with no implementation registered. |

## Synthetic or mocked (demo only, always labelled)

| What | Detail | Where the label shows |
| --- | --- | --- |
| 50 demo learners and their activity | Views, starts and completions per course, scaled from each course's live counts, so popularity and completion factors have something to rank. | Demo data only; never shown as named people |
| Learner feedback ("Did you take it?" answers) | Synthetic survey answers for the grade's feedback factor. | **Not labelled in the interface**: the reviewer's feedback panel shows them like real answers. Say so when presenting, or label them before a wider demo (see LIMITATIONS-AND-NEXT-STEPS.md). |
| Pathway chains | 239 synthetic completions: course → impact action → course, one chain per goal, so "usually go on to" can appear. | "Demo data: synthetic learners" on the card |
| Outcome figures in the demo | Computed from the synthetic learners above. | The outcome note ends "Demo data: these figures come from synthetic demo learners, not real youth." |
| 8 named demo courses | Courses 101–108 (ids `c0a7e000-0000-4000-8000-000000000101` to `…108`, `seed/demo-courses.json`) with local stand-in pages (`scripts/course-quality-demo/fixtures/`) used when the real page does not give the expected result, so the walkthrough is repeatable. | Not marked in the course list; the demo's start summary lists them |
| Demo partner organisation | `c0a7e000-0000-4000-8000-000000000001`, owner of the named courses. | — |
| Local sign-ins | Keycloak test accounts from Yoma's own local seed. | — |
| Preview pages | `/my-yoma-preview`, `/ziqi-draft-preview`, `/ziqi-admin-preview` render fixtures without the API; 404 in production unless the demo flag is on. | Page headings say preview |

## Not built yet (reserved, shows an honest empty state)

| What | What the youth or admin sees |
| --- | --- |
| Drop-off per path step | "Collecting from the pilot". Step events are recorded from now on; no figure is shown until `Pathway:DropOffAvailable` is on and at least 20 youth took a step. |
| Employment outcomes | "Unknown" while Yoma has no job completions; never 0%. |
| ZLTO balance on the My Yoma YoID card | "–" (reading it needs a call to the ZLTO provider on every page view). |
| v5 credential schemas | Mapping prepared in `docs/work/active/course-quality-judger/data-mapping.md`; schemas pending from Yoma. |
