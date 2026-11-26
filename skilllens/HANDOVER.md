# SkillLens: handover note for GenU

SkillLens is **Phase 3 of the BBS x GenU journey**. After a course, the learner takes a 10-minute challenge, gets feedback against a rubric, revises once, and on a pass records a **Skill Credential** on their YoID for YoCV to use. The prototype follows the team's shared sample learner, Halima Y.:

- **Course:** Solar PV Installation Basics (`opp-course-001`)
- **Challenge:** `chal-001`
- **Credential on her YoID:** `cred-002`
- **Her YoID:** `yoid-ng-7f3a0123`
- **Handoff in:** Learning Plan `plan-001` from PathFinder
- **Handoff out:** Skill Credential `cred-002` to YoCV

All sample data is fictional and labelled on screen.

**Live demo:** https://skilllens-gamma.vercel.app/demo/p2e (simulated AI). **Code:** this repository, wongkycharisse/skilllens.

## Where to find things

| What | Where |
| --- | --- |
| Setup, commands, environment variables | [README.md](README.md): Quick start. Node 24, `npm ci`, `npm run setup`, `npm run dev`. No accounts or keys needed. |
| How it connects to Yoma and P2E, and what a live GenU environment needs | [INTEGRATION.md](INTEGRATION.md), sections 6 to 8 (section 8: the YoID credential design) |
| Prompts | [`prompts/v3/`](prompts/v3): `generate.md`, `feedback.md`, `vision.md`, `safety.md` (active). `v1` and `v2` are earlier tested sets. |
| Rubrics | [`data/synthetic/rubrics.json`](data/synthetic/rubrics.json): one per course, 3 or 4 criteria, each level described in words, in English and Pidgin. The course tools each challenge looks for are in [`templates.json`](data/synthetic/templates.json) (`tools`). The pass rule is in [`lib/credential.ts`](lib/credential.ts). |
| Challenges | [`data/synthetic/templates.json`](data/synthetic/templates.json): 9 challenges, each with its lesson, a "Remember from the lesson" recap and step-by-step help |
| Test cases | [`data/synthetic/samples.json`](data/synthetic/samples.json): 180 scored answers (90 prompt examples, 90 held out for the eval). [`data/synthetic/redteam.json`](data/synthetic/redteam.json): 10 harmful, personal-detail and injection inputs. [`scripts/scenarios.ts`](scripts/scenarios.ts): 21 realistic, edge, safety and failure scenarios, including Halima's journey (R6). `*.test.ts`: unit and route tests. [`docs/test-results/`](docs/test-results): the latest real-model run. |
| Shared mock data | [`data/shared/shared-mock-data.json`](data/shared/shared-mock-data.json), v1.1, copied unchanged from Anaghataneja-stack/pathfinder-prototype, and read through [`lib/shared.ts`](lib/shared.ts) |
| YoID thread (mocked) | [`lib/yoid.ts`](lib/yoid.ts), `GET /api/yoid`, `POST /api/yoid/credential` |
| Design tokens | [`app/yoma-tokens.css`](app/yoma-tokens.css) (Yoma's values, copied from Yoma's web app) mapped in [`app/globals.css`](app/globals.css) |
| Progress log | [docs/PROGRESS.md](docs/PROGRESS.md) |

## Secrets

- No key, token or password is committed. A scan of the whole git history found none.
- `.env.local` is git-ignored. [`.env.example`](.env.example) lists every setting, with safe defaults (simulated mode, public Yoma stage address) and every key and password left empty.
- `npm run setup` makes a random facilitator password and pass-signing secret on your machine.
- The public demo needs `PASS_SIGNING_SECRET` set as a Vercel environment variable, so a pass scored on one server instance is accepted on another. It is set there, never in the repo.
- The public demo on Vercel has no AI or Yoma keys: it runs in simulated mode.
- For Azure, put the keys in a vault (for example Azure Key Vault) and set them as environment variables (README: Environment variables).

## What is live, rule-based and mocked

| Part | Status | Detail |
| --- | --- | --- |
| Challenge details, feedback, photo reading | **Live AI** locally; **rule-based** on the public demo | A real model call through Ollama (`gemma3:4b`) on a laptop, or Azure OpenAI when keys are set (`AI_PROVIDER`). With no AI, the rule-based answer check below does the scoring. |
| Answer check | **Rule-based** | `lib/evidence.ts` reads each answer for causes, amounts and sums, concrete steps, polite wording and course terms. It keeps the AI's levels within one step of what the answer shows, so feedback never asks for something already there. |
| "I'm confused" answers | **Rule-based** | English and Pidgin phrases. The question is explained step by step instead of being scored. |
| Pass rule | **Rule-based** | Every criterion Good or Strong at Level 2. Proposed; educators have not approved it. |
| Content safety | **Live AI** locally (keyword list, then the model as a classifier); **stub** on the public demo | Azure AI Content Safety is built in behind `CONTENT_SAFETY_PROVIDER=azure`. |
| Personal-detail stripping | **Rule-based** | Phones, emails, names, addresses and handles are removed before any model call. |
| Yoma skills | **Live** | Yoma's public skill lookup (stage), cached in `data/yoma/skills.json` |
| "Next steps on Yoma" | **Live** for the two other sample courses; **sample data** for Halima's course | Halima's course shows the shared job `opp-job-001`. |
| Practice sample to a Yoma profile | **Mocked** (preview) | The exact Yoma request (`PUT /myOpportunity/action/{id}/verify`) is built but not sent. |
| Learning Plan from PathFinder | **Sample data** | Read from the shared file, not received from PathFinder |
| YoID thread and the Skill Credential write | **Mocked** | In memory. The Saved Opportunity and Learning Plan records come from the shared file. SkillLens writes `cred-002` only for the shared challenge `chal-001`, with a signed one-time pass the feedback route issues when it scores a Level 2 pass, after the learner's tap. The other sample courses never write to Halima's YoID. |
| Learner identity | **Sample data** | Halima is chosen by a URL parameter (`learner=yp-001`). There is no sign-in. |
| Flags and ratings | **Mocked store** | In memory for 72 hours, shown in `/facilitator` behind a shared password |

`/status` in the running app shows which parts are real at that moment.

## What we need from you

1. **Azure keys** (GenU tenant):
   - Azure OpenAI with a text and vision deployment, and Azure AI Content Safety, to run real AI on the hosted demo
   - Until then, real AI runs only on a laptop with Ollama.
2. **Yoma access and decisions** (INTEGRATION.md section 8):
   - Setup: an organisation-admin account; one opportunity per course with credential issuance on; P2E added as a Partner Sync partner with verification scope; a Yoma sign-in client for the host page
   - Confirm: which credential fields appear for learners under 18 (Yoma's schema includes date of birth); how a wrong credential is withdrawn
3. **The handoff contract between the four prototypes:**
   - how PathFinder hands `plan-001` to SkillLens, and how YoCV reads `cred-002` (format and sign-in)
   - Today all four read the same JSON file.
4. **Educator material:**
   - approved rubrics, and about 20 educator-scored answers per challenge to replace the synthetic ones
   - an agreed agreement bar (for example QWK of at least 0.7 per criterion) before any credential is issued without a person checking it
   - P2E lesson access: lesson text needs a login, so challenge wording is ours
5. **Language:**
   - Halima speaks Hausa, but SkillLens has English and Nigerian Pidgin only. Hausa needs native-speaker translation and a decision on whether to add it.
   - The Pidgin text also needs native-speaker review.
6. **Design kit:**
   - Yoma has no published component package, so SkillLens copies Yoma's tokens and mirrors its components.
   - PathFinder uses its own green palette, so the four prototypes do not yet share one look. Please confirm Yoma's tokens as the shared kit, or send the kit the others should use.
7. **Repository access:**
   - This repository is private. Give GenU reviewers access, or make it public.
   - The public copy in Devin-Fitzpatrick06/yoma#1 is several updates behind.
8. **Safeguarding:** UNICEF child-safeguarding and data-protection sign-off before any real learner uses it.

## Known limitations

- **Small model:** the local model is small (`gemma3:4b`) and slow (about 10 to 20 seconds per feedback), and the public demo has no AI at all. On the latest real-model run (docs/test-results/scenarios-local-model.md, 25 Nov 2026), 20 of 21 scenarios passed, including Halima's journey. The one failure was the model stuck repeating itself on all three retries, which happens now and then with this model.
- **Agreement numbers:** these come from synthetic answers and the simulated scorer only (QWK 0.71 Solar, 0.61 Financial Literacy, 0.51 Job Readiness), and are weak for some criteria (Job Readiness "understanding" 0.23). The real model has not been measured on real educator-scored answers.
- **Rule-based checks:** keyword rules for English and Pidgin, so a strong answer in unusual words can read one level low.
- **Mocked state:** the YoID thread, flags and ratings are in memory per server instance. On the public demo a write can be lost on a restart, or not show on another instance. The screen uses what the write returns, so the demo still works.
- **No sign-in:** anyone with the link sees Halima, and her mocked YoID record is shared by every visitor on a server instance. A credential is only recorded after the server itself scored a pass.
- **Level 2 check is advisory:** the browser says when an answer is a second attempt, so the server cannot be sure the learner saw Level 1 feedback first. Every criterion must still be Good or Strong in the server's own scoring. A live version should keep the attempt on the server.
- **Replay, not script:** the demo scores what is typed, so it follows the shared story (attempt 1 not yet, attempt 2 passed) only when the answers are like Halima's. It does not force that outcome.
- **One solar challenge:** there is only the shared `chal-001`, so a learner who retries sees the same task.
- **Simulated photos:** with no vision model, a photo returns a labelled note asking the learner to type what their notes say. In the shared story, Halima's attempt 2 is a photo of handwritten notes, which only works with a real vision model.
- **No rate limiting:** nothing limits the API routes, which a public deployment needs.
- **Accessibility:** checked by hand (contrast, focus order, labels, 48 px targets) at 375 px and 1280 px widths. Yoma's own style puts white icons on orange (1.9:1); SkillLens uses purple icons on orange (7.1:1) instead. No screen-reader user test yet.

## Suggested next steps

1. **Real AI on Azure:** set the Azure keys, run `npm run scenarios` and `npm run eval` on the real model, and record the results.
2. **Educator review:** replace the synthetic rubrics and answers with educator-approved ones, then agree the pass rule and the agreement bar.
3. **Wire the handoffs:**
   - SkillLens reads `plan-001` from PathFinder.
   - YoCV reads `cred-002` from YoID, through the same YoID record format (`lib/yoid.ts`, `HandoffRecord`).
4. **Pilot with a person approving each pass:** use Yoma's learner-submitted verification and an organisation admin approving it (INTEGRATION.md section 8, option B). Move to Partner Sync once agreement meets the bar.
5. **Hausa:** add Hausa challenges and interface text with native-speaker review.
6. **Before any real learners:**
   - a database with a 72-hour expiry for flags and ratings
   - rate limiting
   - facilitator sign-in through Yoma
   - a screen-reader test
