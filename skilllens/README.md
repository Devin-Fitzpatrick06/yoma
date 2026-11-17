# SkillLens prototype

An optional 10-minute AI "level-up" challenge shown at the end of a Passport to Earning (P2E) course. Built for UNICEF / Generation Unlimited by the Berkeley Business Society team.

**Status (16 November 2026):** the full learner journey works end to end with **real AI calls** (a local model through Ollama, or Microsoft Foundry when keys are set) and **live Yoma data** (skills and opportunity search). Open `/status` in the running app to see which integrations are real and which are simulated right now.

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
npm run check       # lint, types, 76 unit and route tests, offline eval, production build (no model needed)
npm run eval        # agreement and safety report with the model in .env.local
npm run scenarios   # 20 realistic, edge-case and failure scenarios with the model in .env.local
```

GitHub Actions runs `npm run check` on every push.

## The demo

`/demo/p2e` is a mock P2E "course complete" page in P2E's own look. It opens SkillLens in an iframe:

1. Choose a language (English or Nigerian Pidgin).
2. Read the scenario.
3. Answer by typing, speaking (Chrome's Web Speech API) or taking a photo of handwritten work. Every mode fills the same editable text box.
4. Feedback: "Did well" and "To improve". The learner never sees a score. An optional 1–5 "Was this helpful?" rating follows.
5. Level 2: revise with the tip shown, then get feedback again.
6. Save: the Level 2 answer is stored as "Practice sample, not a certification", shown with the Yoma skills it practised, and sent to the host page.
7. "Add to my Yoma profile" builds the exact Yoma request. In mock mode it shows "Ready for Yoma (demo: nothing was sent)".

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

`npm run eval` runs the 80 held-out educator-scored answers (never used as prompt examples) through the same pipeline learners use. It reports, for each rubric criterion:

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
| Financial literacy | 45% | 90% | 0.51 |
| Job readiness | 55% | 86% | 0.56 |

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
| `POST /api/feedback` | `{templateId, lang, values, answer}` → `{status: "ok", strengths, suggestion, answer (cleaned), redacted, skills}` or `{status: "flagged"}`. Scores stay on the server. |
| `POST /api/transcribe` | form field `photo` → `{status, text}`. The photo is screened, compressed on the device and never stored. |
| `POST /api/rating` | `{templateId, level, rating: 1-5}` |
| `POST /api/yoma` | `{sample}` (+ optional `Authorization: Bearer <learner Yoma token>`) → a mock preview or a live Yoma result |
| `GET /api/yoma/opportunities?course=` | "Next steps on Yoma": `{source: "live" \| "snapshot", items}` from Yoma's public opportunity search |
| `GET /api/status` | Which integrations are real or simulated right now, with reachability |

Errors come back as `{error}` with status 400 (bad request) or 502 (AI, safety or Yoma unavailable). The learner's browser times out after 45 s, retries once and shows a "Try again" button.

## How the AI is used

Prompts live in [`prompts/v2/`](prompts/v2) (active) so educators can review them; [`prompts/v1/`](prompts/v1) is kept as the first tested set. To change the wording, create a new version folder and bump `PROMPT_VERSION` in `lib/ai/prompts.ts`. v2 fixed three problems found by `npm run scenarios`: praise for ideas the learner never wrote, a cheating attempt flagged as harmful, and business slang ("kill the competition") flagged as violent.

- **Challenge** (`generate.md`): the model only picks short local values for the template's `{slots}` (names, goods, naira prices). Our code fills in the educator-approved wording; the model never writes it. Values are checked: max 40 characters, no `{ } < >`.
- **Feedback** (`feedback.md`): the rubric and the 10 educator-scored examples go in the prompt. The learner's answer, with personal details removed, goes in a separate, delimited message. The JSON reply is validated with Zod and must cover exactly the rubric's criteria.
- **Photo** (`vision.md`): a vision-capable deployment transcribes the handwriting, and the learner checks the text before submitting.

- **Safety** (`safety.md`, with `CONTENT_SAFETY_PROVIDER=model`): the keyword list first, then the model rates Hate, SelfHarm, Sexual and Violence from 0 to 6; 2 or more is flagged.

Every call goes through `lib/pipeline.ts`: strip personal details → screen input → model → screen output → drop any "Did well" item that shares no words with the answer (`lib/safety/grounding.ts`). Flagged model output is replaced with the educator-written rubric text. Model calls retry up to twice on invalid JSON, a 429 or a 5xx, each retry at a higher temperature (0, 0.3, 0.6), because at temperature 0 a small model can loop on one token and fail the same way again.

## Layout

```
app/
  demo/p2e/             Mock P2E host page
  embed/skilllens/      The embeddable learner journey (iframe route)
  facilitator/          Flagged cases + helpfulness ratings (password protected by middleware.ts)
  api/                  challenge, feedback, transcribe, rating, yoma
components/             SkillLens, AnswerInput, FeedbackPanels
lib/
  pipeline.ts           The one safe path for every input and output
  safety/pii.ts         Personal-detail stripping
  safety/contentSafety.ts  Azure AI Content Safety + offline stub
  store.ts              Flags (72 h) and ratings, in memory
  ai/                   Provider interface, mock and Foundry providers, prompt loader
  eval/metrics.ts       Exact / adjacent agreement and quadratic weighted kappa
  yoma/                 Skill mapping, evidence PDF + CSV, Yoma client
data/
  synthetic/            SYNTHETIC PLACEHOLDER content for educators to replace (courses, rubrics, templates, samples, redteam)
  yoma/                 skill-map.json (editable) and skills.json (cached Yoma ids)
prompts/v1/             Versioned prompts
scripts/                seed, eval, yoma-skills
```

## About the synthetic data

Everything in `data/synthetic/` is placeholder content written for the demo, not real P2E material.

- The Pidgin text needs review by a native speaker.
- The sample answers are built from fragments. Their scores imitate an educator, including some disagreement.
- Replace them with real educator-scored answers before trusting any evaluation numbers.

## Design

The look matches the Passport to Earning site:

- **Colours:** blue `#0a59a7` and yellow `#ffcb03`, with light-blue tints.
- **Fonts:** Oswald headings and Jost body text, self-hosted by `next/font`, so the learner's phone never calls Google.
- **Shapes:** pill-shaped buttons, rounded cards, and round line icons (`components/Icon.tsx`, inline SVG, no icon font).
- **Tokens:** every colour and font is a token in `app/globals.css`, so a Yoma theme is a one-block change.

Accessibility:

- Tap targets are 48 px and every input has a label.
- White on blue has a contrast ratio of 7.0:1, blue on yellow 4.6:1, and body text 14.7:1.
