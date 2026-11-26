# SkillLens prototype

An optional 10-minute AI "level-up" challenge shown at the end of a Passport to Earning (P2E) course. Built for UNICEF / Generation Unlimited by the Berkeley Business Society team.

**Status (16 November 2026):** the full learner journey works end to end with **real AI calls** (a local model through Ollama, or Microsoft Foundry when keys are set) and **live Yoma data** (skills and opportunity search). Open `/status` in the running app to see which integrations are real and which are simulated right now.

It is **Phase 3 of the BBS x GenU journey** (Ziqi → PathFinder → SkillLens → YoCV). It uses the team's shared mock data, so the same learner, job, course and credential appear in all four prototypes: Halima Y., the Solar Installation Assistant job `opp-job-001`, the Solar PV Installation Basics course `opp-course-001`, and the Skill Credential `cred-002`, which SkillLens records on her YoID (mocked).

- **Handover note (what is live, rule-based and mocked; what you need to provide; limitations; next steps): [HANDOVER.md](HANDOVER.md)**
- Development progress, test results and next sprint: **[docs/PROGRESS.md](docs/PROGRESS.md)**
- For P2E and Yoma engineers: **[INTEGRATION.md](INTEGRATION.md)**

**Live demo:** https://skilllens-gamma.vercel.app/demo/p2e (status: https://skilllens-gamma.vercel.app/status). It runs simulated AI and safety, because no Azure keys are set there. For real AI, run it locally with Ollama (below).

## Quick start

Needs Node 24 (`.nvmrc`) and git.

This repository is private: ask the owner for access, or use the public copy in the `skilllens/` folder of [Devin-Fitzpatrick06/yoma#1](https://github.com/Devin-Fitzpatrick06/yoma/pull/1) (`cd skilllens` there, then the same steps). The sample data the app needs is in the repo (`data/synthetic`, `data/yoma`), so there is nothing to download. What connects to Yoma, and what a live GenU environment needs: [INTEGRATION.md](INTEGRATION.md) sections 6 and 7.

```bash
git clone https://github.com/wongkycharisse/skilllens.git
cd skilllens
npm ci
npm run setup     # writes .env.local: real local AI if Ollama is running, otherwise simulated
npm run dev       # open http://localhost:3000/demo/p2e in your browser's phone view
```

`npm run setup` never overwrites an existing `.env.local`. It prints which mode it chose. No accounts or keys are needed for either mode.

### Real AI with no account (recommended for demos)

1. Install Ollama from [ollama.com](https://ollama.com) (free, about 220 MB).
2. `ollama pull gemma3:4b` (about 3.3 GB; reads text and photos).
3. Delete `.env.local` if you already have one, then `npm run setup` again. It picks `AI_PROVIDER=local` and `CONTENT_SAFETY_PROVIDER=model`.

Tested on an Apple M5 laptop with 16 GB of memory. Feedback takes about 6 s per answer plus about 1.5 s for each safety check.

### Real AI on Azure (the target for GenU)

Set `AI_PROVIDER=foundry` and the `FOUNDRY_*` variables, and `CONTENT_SAFETY_PROVIDER=azure` with the `CONTENT_SAFETY_*` variables, in `.env.local`. The code path is the same as the local model; only the endpoint changes.

### Check everything

```bash
npm run check       # lint, types, 99 unit and route tests, offline eval, production build (no model needed)
npm run eval        # agreement and safety report with the model in .env.local
npm run scenarios   # 20 realistic, edge-case and failure scenarios with the model in .env.local
```

GitHub Actions runs `npm run check` on every push.

## The demo

`/demo/p2e` is a mock "course complete" page on Yoma, labelled as a mock-up with sample data. It follows Halima (`yp-001`, YoID `yoid-ng-7f3a0123`) after she finishes Solar PV Installation Basics:

- **Learning Plan `plan-001`** (the handoff item from PathFinder): Working Safely at Height → Solar PV Installation Basics (you are here) → apply for `opp-job-001`.
- **Halima's YoID thread**: one record per handoff item: Saved Opportunity `opp-job-001` (Ziqi), Learning Plan `plan-001` (PathFinder), Skill Credential `cred-002` (SkillLens, waiting until she passes), Job Application `app-001` (YoCV, next).
- **Challenge yourself** opens SkillLens in an iframe on the shared challenge `chal-001`: "A customer's solar light stopped charging. List three things you would check, in order."

The other two sample courses (Financial Literacy, Job Readiness) stay in the course menu. In SkillLens:

1. Choose a language (English or Nigerian Pidgin).
2. Read the challenge and the "Remember from the lesson" facts it relies on.
3. Answer by typing, speaking (Chrome's Web Speech API) or taking a photo of handwritten work. Every mode fills the same editable text box.
4. Feedback: "Did well" (quoting the learner's own words), one "To improve" tip, and "How you are doing": the 3 or 4 rubric criteria, each shown as a level in words (Just starting, Getting there, Good, Strong) with what we saw and what the next level looks like. No numbers or total. An optional 1–5 "Was this helpful?" rating follows.
5. Level 2: revise with the tip shown, then get feedback again. The Level 1 answer and levels go with it, so the feedback says what got better ("↑ Up from Getting there") and moves to a new tip once the old one is acted on.
6. Save: the Level 2 answer is stored as "Practice sample, not a certification", shown with the Yoma skills it practised, and sent to the host page.
7. If every part of the Level 2 answer is Good or Strong, the learner can tap "Add Skill Credential to my YoID" (consent). That writes `cred-002` ("Solar PV Installation Basics: practical challenge passed", skill "Solar panel installation") to Halima's YoID, mocked, and the host page's YoID thread updates. It is accepted only with the one-time pass id the server gave when it scored the pass. Otherwise, "Add to my Yoma profile" builds the exact Yoma request for the practice sample ("Ready for Yoma (demo: nothing was sent)").

Halima's story in the shared data: attempt 1 is "not yet" (the tip asks how she would test the panel's output), and attempt 2 passes and issues `cred-002`.

Things to try:
- Type a phone number or "my name is …" into an answer: it is removed before feedback.
- Type "I want to end my life": the answer is not sent for feedback, the learner sees a supportive message, and the case appears in the facilitator view.

**Facilitator view:** `/facilitator`, with any username and the `FACILITATOR_PASSWORD` from `.env.local`. It lists flagged cases (kept 72 hours, personal details removed) and the helpfulness ratings.

## Commands

| Command | What it does |
| --- | --- |
| `npm run setup` | Write `.env.local` for a fresh clone (real local AI if Ollama is running, else simulated) |
| `npm run dev` | Start the dev server |
| `npm test` | Unit and route tests (Vitest), no network |
| `npm run eval` | AI–educator agreement and safety report with the configured model, saved to `reports/` |
| `npm run eval:mock` | The same eval in simulated mode (used by `check`) |
| `npm run scenarios` | 20 realistic, edge-case and failure scenarios, saved to `reports/` |
| `npm run check` | Lint, type check, tests, offline eval and production build: run before every commit |
| `EVAL_CONCURRENCY=2 npm run eval` | Fewer parallel model calls (use 1-2 for a laptop model; default 4) |
| `npm run seed` | Regenerate `data/synthetic/samples.json` from the answer fragments |
| `npm run yoma:sync` | Refresh Yoma skill ids and the opportunity snapshot from Yoma's public API |
| `npm run build` / `npm start` | Production build and server |

## Evaluation

`npm run eval` runs the 90 held-out educator-scored answers (10 per challenge) (never used as prompt examples) through the same pipeline learners use. It reports, for each rubric criterion:

- **exact agreement:** AI score equals the educator's
- **adjacent agreement:** within one level
- **quadratic weighted kappa (QWK):** 1 is perfect, 0 is chance

It also runs `data/synthetic/redteam.json` (harmful, personal-detail and prompt-injection inputs) and counts flags. It **exits 1** if any of these fail:
- every harmful input is flagged
- no personal details reach the model
- no prompt injection gets top marks
- there are no errors or invalid model output

Mock provider results on the synthetic data:

| Rubric | Exact | Within 1 | QWK |
| --- | --- | --- | --- |
| Solar PV Installation Basics | 57% | 93% | 0.71 |
| Financial literacy | 51% | 89% | 0.61 |
| Job readiness | 46% | 84% | 0.51 |

All safety gates pass. These numbers measure the mock and the synthetic data, not a real model. Set `AI_PROVIDER=foundry` to measure the model you deploy.

The learner helpfulness rating is collected in the app (`POST /api/rating`) and shown in `/facilitator`. An offline eval has no ratings.

## Environment variables

All are in `.env.example`. Never commit real keys: `.env.local` is git-ignored.

| Variable | Default | Needed for |
| --- | --- | --- |
| `AI_PROVIDER` | `mock` | `local` (Ollama on this machine) or `foundry` (Microsoft Foundry) for real model calls |
| `LOCAL_AI_BASE_URL`, `LOCAL_AI_MODEL`, `LOCAL_AI_VISION_MODEL` | `http://localhost:11434/v1`, `gemma3:4b` | local |
| `AI_TIMEOUT_MS` | 90000 local, 30000 foundry | per model call |
| `FOUNDRY_BASE_URL`, `FOUNDRY_API_KEY`, `FOUNDRY_CHAT_DEPLOYMENT` | | foundry |
| `FOUNDRY_VISION_DEPLOYMENT` | chat deployment | a separate vision deployment for photos |
| `CONTENT_SAFETY_PROVIDER` | `stub` | `model` (keywords + the AI model, real, text only) or `azure` (Azure AI Content Safety, text and images). **The stub is not a safety system.** |
| `CONTENT_SAFETY_ENDPOINT`, `CONTENT_SAFETY_KEY` | | azure |
| `CONTENT_SAFETY_THRESHOLD` | `2` | flag at severity 2, 4 or 6 |
| `FACILITATOR_PASSWORD` | | opening `/facilitator` (it stays closed until set) |
| `PASS_SIGNING_SECRET` | random, from `npm run setup` | signs the one-time pass a Level 2 pass earns, so the mocked YoID write accepts only passes the server scored. Set it on any host with more than one server instance (Vercel). |
| `NEXT_PUBLIC_P2E_ORIGIN` | same origin | the P2E origin allowed to embed SkillLens and receive saved samples |
| `YOMA_MODE` | `mock` | `live` sends to Yoma with the learner's own token |
| `YOMA_API_BASE_URL` | Yoma stage | Yoma API base, ending in `/api/v3` |
| `YOMA_OPPORTUNITY_IDS` | | live: `financial-literacy:<guid>,job-readiness:<guid>` |
| `YOMA_OPPORTUNITIES` | `live` | `snapshot` to show only the saved opportunity list (offline demos) |

Restart `npm run dev` after changing `.env.local`.

## API routes

| Route | Purpose |
| --- | --- |
| `POST /api/challenge` | `{course, lang}` → a filled challenge (screened) |
| `POST /api/feedback` | `{templateId, lang, values, answer, previous?}` → `{status: "ok", strengths, suggestion, levels, rubric, answer (cleaned), redacted, skills, passId}`, or `{status: "flagged" \| "too_short" \| "confused"}`. `passId` is set only for a Level 2 pass. |
| `POST /api/transcribe` | form field `photo` → `{status, text}`. The photo is screened, compressed on the device and never stored. |
| `POST /api/rating` | `{templateId, level, rating: 1-5}` |
| `POST /api/yoma` | `{sample}` (+ optional `Authorization: Bearer <learner Yoma token>`) → a mock preview or a live Yoma result |
| `GET /api/yoma/opportunities?course=` | "Next steps on Yoma": `{source: "live" \| "snapshot" \| "sample", items}` from Yoma's public opportunity search (`sample`: the shared job `opp-job-001` for Halima's course) |
| `GET /api/status` | Which integrations are real or simulated right now, with reachability |
| `GET /api/yoid?id=` | MOCKED: the sample learner's YoID thread, one record per handoff item |
| `POST /api/yoid/credential` | MOCKED: `{yoidId, passId}` → records the Skill Credential once, only for a pass the server scored (409 otherwise) |

Errors come back as `{error}` with status 400 (bad request) or 502 (AI, safety or Yoma unavailable). The learner's browser times out after 45 s, retries once and shows a "Try again" button.

## How the AI is used

Prompts live in [`prompts/v3/`](prompts/v3) (active) so educators can review them; [`prompts/v1/`](prompts/v1) and [`prompts/v2/`](prompts/v2) are kept as earlier tested sets. v3 sends the Level 1 answer with the Level 2 one, and tells the model never to suggest adding something the answer already has. To change the wording, create a new version folder and bump `PROMPT_VERSION` in `lib/ai/prompts.ts`. v2 fixed three problems found by `npm run scenarios`: praise for ideas the learner never wrote, a cheating attempt flagged as harmful, and business slang ("kill the competition") flagged as violent.

- **Challenge** (`generate.md`): the model only picks short local values for the template's `{slots}` (names, goods, naira prices). Our code fills in the educator-approved wording; the model never writes it. Values are checked: max 40 characters, no `{ } < >`.
- **Feedback** (`feedback.md`): the rubric and the 10 educator-scored examples go in the prompt. The learner's answer, with personal details removed, goes in a separate, delimited message. The JSON reply is validated with Zod and must cover exactly the rubric's criteria.
- **Photo** (`vision.md`): a vision-capable deployment transcribes the handwriting, and the learner checks the text before submitting.

- **Safety** (`safety.md`, with `CONTENT_SAFETY_PROVIDER=model`): the keyword list first, then the model rates Hate, SelfHarm, Sexual and Violence from 0 to 6; 2 or more is flagged.

Every call goes through `lib/pipeline.ts`: strip personal details → screen input → model → check the levels against what the answer contains (`lib/evidence.ts`: amounts and sums, causes, concrete steps, polite wording, course terms; each level stays within one step of that evidence, and the tip moves to the weakest criterion) → screen output → drop any "Did well" item that shares no words with the answer (`lib/safety/grounding.ts`). In simulated mode the same evidence check is the scorer, so feedback still responds to what the learner wrote. Flagged model output is replaced with the educator-written rubric text. Model calls retry up to twice on invalid JSON, a 429 or a 5xx, each retry at a higher temperature (0, 0.3, 0.6), because at temperature 0 a small model can loop on one token and fail the same way again.

## Layout

```
app/
  demo/p2e/             Mock course host page on Yoma (Halima's journey, sample data)
  yoma-tokens.css       Yoma's design tokens (colours, fonts), copied from Yoma's web app
  globals.css           Maps the tokens to SkillLens names; no raw colour or font anywhere else
  embed/skilllens/      The embeddable learner journey (iframe route)
  facilitator/          Flagged cases + helpfulness ratings (password protected by middleware.ts)
  api/                  challenge, feedback, transcribe, rating, yoma, yoid (mocked)
components/             SkillLens, AnswerInput, FeedbackPanels, CredentialHandoff, Icon
lib/
  pipeline.ts           The one safe path for every input and output
  shared.ts             The BBS x GenU shared mock data (validated)
  yoid.ts               MOCKED YoID thread and the Skill Credential write
  credential.ts         Pass rule: every criterion Good or Strong at Level 2
  evidence.ts           Rule-based reading of each answer (checks and scores the AI)
  safety/pii.ts         Personal-detail stripping
  safety/contentSafety.ts  Azure AI Content Safety + offline stub
  store.ts              Flags (72 h) and ratings, in memory
  ai/                   Provider interface, mock and Foundry providers, prompt loader
  eval/metrics.ts       Exact / adjacent agreement and quadratic weighted kappa
  yoma/                 Skill mapping, evidence PDF + CSV, Yoma client
data/
  shared/               shared-mock-data.json, copied unchanged from the team (v1.1)
  synthetic/            SYNTHETIC PLACEHOLDER content for educators to replace (courses, rubrics, templates, samples, redteam)
  yoma/                 skill-map.json (editable) and skills.json (cached Yoma ids)
prompts/v1–v3/          Versioned prompts (v3 active)
scripts/                setup, generate-samples (seed), eval, scenarios, yoma-sync
```

## About the sample data

`data/shared/shared-mock-data.json` is the BBS x GenU shared mock data (v1.1), copied unchanged from [Anaghataneja-stack/pathfinder-prototype](https://github.com/Anaghataneja-stack/pathfinder-prototype) `mockups/`. It is fictional. SkillLens uses its learner (`youngPerson`), `learningPlan`, `skillChallenge` (`chal-001`, used word for word), `credentials` (`cred-002`), `opportunities` and `skills`. If the team updates the file, copy the new version over it and run `npm test`: the tests check the ids SkillLens relies on.

Everything in `data/synthetic/` is placeholder content written for the demo, not real P2E material. The Solar PV course, its rubric (3 criteria) and its 20 sample answers were written for the shared challenge; the course itself is fictional in the shared data.

The challenges and rubrics are now built from the real P2E lesson lists on the public catalogue (global.passport2earning.org, read 18 November 2026). Each challenge names its lesson on screen:

| Course | Lessons the challenges practise |
| --- | --- |
| Financial Literacy | Khan Academy *Financial Literacy 101*: Budgeting and the 50:30:20 rule, Paying Yourself First, Emergency Funds, The Importance of Regular Tracking, Predatory Lending. Microsoft *Business 101: Budgeting* (income statement, small-business case studies). |
| Job Readiness | *How to get a Job*: Research the Job and Company, The STAR Method, Things To Be Careful About (online job posts). UNICEF *Communication Skills*: Active Listening and Empathy, Emails in the Workplace, Professional Communication, Conflict Resolution. |

Lesson text and quizzes need a P2E login, so the wording is ours. Educators should check it against the lessons.

- The Pidgin text needs review by a native speaker.
- The sample answers are built from fragments. Their scores imitate an educator, including some disagreement.
- Replace them with real educator-scored answers before trusting any evaluation numbers.

## Design

The look is Yoma's (yoma.world), from Yoma's own design tokens:

- **Tokens:** `app/yoma-tokens.css` holds Yoma's colour and font values, copied unchanged from Yoma's web app (`src/web/src/styles/colors.css` and `globals.css`). `app/globals.css` maps them to SkillLens names (`brand` = Yoma purple, `accent` = Yoma orange, `success` = Yoma green). Components use only those names: there is no hex value, font name or arbitrary spacing anywhere else. Spacing and radii are Tailwind's scale, which Yoma's app also uses.
- **Fonts:** Nunito headings and Open Sans body text, as on yoma.world, self-hosted by `next/font`, so the learner's phone never calls Google.
- **Components:** pill buttons, rounded cards and Yoma's orange circle icons (`btn`, `card`, `icon-circle` in `globals.css`; `components/Icon.tsx`). Yoma has no published component package, so these mirror its look rather than import its React components.

Accessibility:

- Tap targets are 48 px and every input has a label.
- White on purple is 13.7:1, purple on orange 7.1:1, purple on the purple tint 9.8:1, and grey text 7.2:1. Yoma's pink (3.3:1) and its green on light green (4.2:1) are too light for text, so text uses darker mixes of those same tokens (7.9:1 and above 4.5:1). White is never used for text on orange.
