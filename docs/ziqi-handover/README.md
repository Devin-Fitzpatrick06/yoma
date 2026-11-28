# Ziqi handover package

Ziqi is Yoma's opportunity quality auditor, built as a Berkeley Business Society pro bono project. It checks every
published listing (courses, jobs, events, impact actions), grades courses and jobs A–D, rewards partners with badges,
checks a partner's draft before it is published, and builds each young person a career path ("My Yoma") from courses
and practice to a first job.

Everything is on branch `feature/course-quality-judger` of this repository. Nothing here is merged to `master` or
deployed; every new feature is behind a switch that is **off by default** outside the local demo.

## What is in this folder

| File | What it is for |
| --- | --- |
| [SETUP.md](SETUP.md) | Run the demo on a Mac in one command, or run the API and website for development. Configuration, switches, where keys go. |
| [LIVE-RULES-MOCKED.md](LIVE-RULES-MOCKED.md) | What is live data, what is rule-based, what is AI (optional), and what is synthetic or mocked. Read before showing anyone a number. |
| [LIMITATIONS-AND-NEXT-STEPS.md](LIMITATIONS-AND-NEXT-STEPS.md) | Known limitations and the suggested next steps, in order. |
| [prompts/](prompts/) | The AI prompt and output schema used for course checks, exactly as the code sends them. |
| [rubrics/](rubrics/) | Every rubric and rule set as JSON: verification checks, course and job grading, partner and learner badges, pathway goals and questions. |
| [test-cases/](test-cases/) | The test-case inventories (API and web), how to run them, the demo test data, and the hand-rated validation sample. |

The prompt and rubric files are generated from the code's defaults and checked by a test
(`src/api/src/test/Yoma.Core.Test/Handover/HandoverExportTests.cs`): if code and file disagree, the test fails. They
cannot silently drift.

## Where the code lives

| Area | Path |
| --- | --- |
| Checks, grades, reviewer queue, draft check (API) | `src/api/src/domain/Yoma.Core.Domain/CourseQuality/`, `src/api/src/application/Yoma.Core.Api/Controllers/CourseQualityController.cs` |
| AI providers (optional) | `src/api/src/infrastructure/Yoma.Core.Infrastructure.Anthropic/`, `src/api/src/infrastructure/Yoma.Core.Infrastructure.OpenAICompatible/` (Ollama, vLLM, LM Studio, a government host) |
| Link health CLI (any platform) | `scripts/link-health/check-links.mjs` |
| Pathways, outcomes, badges (API) | `src/api/src/domain/Yoma.Core.Domain/Pathway/` (incl. `Badges/`), controllers `Pathway*Controller.cs` |
| Database tables and migrations | `src/api/src/infrastructure/Yoma.Core.Infrastructure.Database/` (`CourseQuality`, `Pathway` schemas; migrations `ApplicationDb_CourseQuality_*`, `ApplicationDb_Pathway_*`) |
| Website | `src/web/src/pages/my-yoma/`, `src/web/src/components/MyYoma/` (incl. `JourneyMap/`), `src/web/src/components/CourseQuality/`, `src/web/src/pages/admin/course-quality/`, the partner listing editor `src/web/src/pages/organisations/[id]/opportunities/[opportunityId]/` |
| Web ↔ API contracts | `src/web/src/api/models/courseQuality.ts`, `src/web/src/api/models/pathway.ts` (the C# models mirror them) |
| Local demo | `scripts/course-quality-demo/` (`start.sh`, `stop.sh`, `ai-smoke.sh`, seeds, validation sample tools) |
| Design notes and session handoffs | `docs/work/active/course-quality-judger/` (start with `handoffs/2026-11-28-i5.md`) |

## Status, iteration 5 (2026-11-28)

Live AI end to end on a **free, locally hosted open model**, verified on 28 November 2026 without any paid AI:

- **AI through the Yoma backend:** `qwen2.5:3b` in Ollama ran through the Yoma API on 8 courses: all answered, 8,839
  tokens in, 2,505 out, **$0.00**. Each run's model, tokens, cost, time and outcome are stored and shown on the reviewer
  card (**Run AI check**) and in the AI usage panel.
- **Swappable model:** set by configuration (`AI:Provider`: any OpenAI-compatible server, or Anthropic, which stays
  optional and billed).
- **Other opportunity types:** live events (17), impact actions (41) and jobs (2,000) are imported and audited.
- **Link health on any platform:** the admin page and a standalone CLI.
- **Speed:** 58 to 98 seconds per AI check on an idle 4-core CPU.
- **Checks:** an independent review found 7 issues; all were fixed and re-checked. API: 1,457 Ziqi tests, all passing
  except the one sandbox-DNS case. Web: 555 tests, type check and lint clean.

Not yet done:

- A timed `start.sh --local-ai` run on a Mac.
- Owner reviews (Adrian for the API, Jason for the website).

Details: `docs/work/active/course-quality-judger/handoffs/2026-11-28-i5.md`.

## Status at handover (2026-11-27)

- API: build with 0 warnings; 1,321 Ziqi tests (course quality, pathways, badges, handover) — all pass except one that
  needs real DNS (`RealResolver_RefusesLocalTargets("http://localhost./")`, fails only in sandboxes without it).
- Web: type check and lint clean; 524 unit tests pass.
- Integrated demo exercised end to end (sign-in → My Yoma → onboarding → journey map; lock, swap; partner draft check;
  admin views), then an independent code review and a test pass (desktop, 390 px, 320 px, keyboard, reduced motion);
  every finding was fixed and re-checked.
- No keys or secrets are committed: the AI key is empty in `appsettings.json`; the demo uses only the public local
  placeholders that Yoma's own `docker-compose.yml` already uses (see SETUP.md).
