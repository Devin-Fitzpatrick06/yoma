# Integrating SkillLens with P2E and Yoma

For the P2E and Yoma engineering teams. Everything here was checked against the Yoma source (`yoma/src/api`) and Yoma's stage API in November 2026.

## 1. Embedding in P2E

P2E shows SkillLens as the last step of a course, in an iframe:

```html
<iframe
  title="SkillLens level-up challenge"
  src="https://<skilllens-host>/embed/skilllens?course=financial-literacy&lang=en"
  allow="microphone"
  style="width:100%;height:85vh;border:0"></iframe>
```

| Parameter | Values |
| --- | --- |
| `course` | `financial-literacy`, `job-readiness` (ids in `data/synthetic/courses.json`) |
| `lang` | `en`, `pcm` (the learner can change it on the first screen) |

Set `NEXT_PUBLIC_P2E_ORIGIN` to the P2E origin (for example `https://unicef.myp2e.org`). It does two things:

- **Framing:** the `Content-Security-Policy: frame-ancestors` header lets only that origin (and SkillLens itself) frame the page.
- **Saved samples:** when the learner saves, SkillLens posts a message to that origin only:

```js
window.addEventListener("message", (e) => {
  if (e.origin !== "https://<skilllens-host>" || e.data?.type !== "skilllens:saved") return;
  const sample = e.data.sample; // see "Practice sample" below
});
```

## 2. Practice sample

The only learner content SkillLens keeps. It is the Level 2 answer, with personal details already removed, and **no scores**.

```json
{
  "courseId": "financial-literacy",
  "templateId": "fl-weekly-saving",
  "lang": "en",
  "scenario": "Amina earns ₦5,000 a week from washing cars in Kano. ...",
  "answer": "Amina should save ₦1,000 every week ...",
  "skills": [{ "id": "e2fc4e8b-d6c5-4f5a-be08-4410289b77bb", "name": "Financial Literacy" }],
  "savedAt": "2026-11-16T07:10:00.000Z",
  "label": "Practice sample, not a certification"
}
```

Schema: `PracticeSample` in `lib/schema.ts`.

## 3. Yoma

### What Yoma offers

Yoma has **no partner endpoint that records a learner's completion**. The partner API (`api/v3/externalpartner`, client-credentials token from Keycloak) can only create users and read who completed an opportunity. So a practice sample can reach a Yoma profile in two ways:

| Route | Who calls it | Endpoint | Status here |
| --- | --- | --- | --- |
| **A. Learner submits** (recommended) | SkillLens, with the learner's own Yoma access token | `PUT /api/v3/myOpportunity/action/{opportunityId}/verify` (multipart) | Built: `lib/yoma/client.ts`, `POST /api/yoma` |
| **B. Bulk CSV** | An org admin in the Yoma admin portal | `POST /api/v3/myOpportunity/action/verify/csv` | Builder ready: `verifyCsv()` in `lib/yoma/evidence.ts` |

### Setup an org admin does once on Yoma

1. Create one opportunity per SkillLens course, for example "SkillLens practice: Financial Literacy".
2. Turn on verification with evidence type **FileUpload**.
3. Attach the skills listed in `data/yoma/skills.json`. Yoma gives the learner the opportunity's skills when it is verified.
4. Put the opportunity ids in `YOMA_OPPORTUNITY_IDS=financial-literacy:<guid>,job-readiness:<guid>`.

### Route A request (what `submitToYoma` sends)

Field names come from `MyOpportunityRequestVerify.cs`:

| Form field | Value |
| --- | --- |
| `Certificate` | `skilllens-practice-sample.pdf`, a one-page PDF labelled "Practice sample, not a certification". Yoma accepts pdf, docx, pptx and images for certificates, up to 10 MB. |
| `DateStart`, `DateEnd` | `savedAt` |
| `Feedback` | `Practice sample, not a certification. SkillLens <course>.` |

The header is `Authorization: Bearer <learner's Yoma access token>`. The host page gets that token through Yoma SSO (see `yoma/docs/sso/README.md`) and forwards it to `POST /api/yoma`. With `YOMA_MODE=mock` (the default), SkillLens builds the exact request and returns a preview without sending it.

### Skills

`data/yoma/skill-map.json` (editable by educators) maps each rubric criterion to Yoma skill names. `npm run yoma:sync` resolves them through Yoma's public `GET /api/v3/lookup/skill` and caches the ids in `data/yoma/skills.json`. The app reads only the cache, so it never depends on Yoma being up. On the server, `POST /api/yoma` replaces any skills sent by the browser with its own mapping.

### Opportunities ("Next steps on Yoma")

After saving, the learner sees up to four Yoma opportunities from the public, anonymous `POST /api/v3/opportunity/search` (`valueContains` per course keyword in `data/yoma/opportunity-search.json`, stage test data filtered out, cached for an hour). If Yoma cannot be reached, the saved snapshot in `data/yoma/opportunities-snapshot.json` is shown and labelled. `npm run yoma:sync` refreshes the snapshot. Yoma's search has no skills filter (`OpportunitySearchFilterBase` filters by type, category, language, country, organisation and text), so results are matched by keywords, not by the practised skills.

### Open questions for the Yoma team

1. Should practice samples be a separate opportunity type, so they never look like a completed course? Route A puts them through normal verification, and an org admin approves them.
2. Would Yoma add a partner endpoint for this, so P2E does not need to hold the learner's Yoma token?
3. Could the public opportunity search accept a `skills` filter, so "Next steps" match the skills a learner practised?
4. Should a practice sample issue an SSI credential? We assume no ("not a certification"), so keep `CredentialIssuanceEnabled` off on these opportunities.

## 4. Safety and data handling (for UNICEF review)

- **Personal details:** every answer and photo transcription is stripped of phone numbers, emails, street addresses, "my name is …" and @handles before it reaches a model, a log or a saved sample (`lib/safety/pii.ts`). This is regex-based, and a named-entity model should be added before scaling.
- **Screening:** every learner input (text and photo) and every model output is screened with Azure AI Content Safety at severity ≥ 2 (`CONTENT_SAFETY_PROVIDER=azure`). If screening is down, the request fails rather than showing unscreened content.
- **On a flag:**
  - Flagged answers never reach the model. The learner sees a supportive message pointing them to a facilitator.
  - Flagged model output is replaced with educator-written rubric text.
- **Storage:**
  - Raw answers and photos are never stored.
  - Flags keep a personal-detail-stripped excerpt of up to 280 characters for 72 hours, in memory, for the password-protected `/facilitator` view.
  - Server logs hold flag ids and categories only.
- **No ranking:** scores stay on the server. Learners see "Did well" and "To improve" only.

## 5. Before a real pilot

| Item | Why |
| --- | --- |
| Azure AI Content Safety resource + keys | The stub is a keyword list for demos, not a safety system |
| Database with a TTL for flags and ratings | The current store is in memory, so it is lost on restart and not shared across instances |
| P2E or Yoma sign-in for facilitators | Replaces the single shared password |
| Real educator-scored answers | Agreement numbers on synthetic data mean nothing |
| Native-speaker review of the Pidgin text | Written by the team, unreviewed |
| Rate limiting on `/api/*` | Protects model spend |

## 6. Path to Yoma integration

Each step builds on code that already exists. Each one changes settings or adds one adapter, not a rewrite.

| Step | What changes | Where | Needs |
| --- | --- | --- | --- |
| 1. Today | Embedded in a mock P2E page. Real skills and opportunities come from Yoma stage. Saving to Yoma is a preview (`YOMA_MODE=mock`). | `app/embed/skilllens`, `lib/yoma/*` | Nothing |
| 2. Yoma stage pilot | An org admin creates the two opportunities (section 3). Set `YOMA_MODE=live`, `YOMA_API_BASE_URL` (stage) and `YOMA_OPPORTUNITY_IDS`. The host page passes the learner's Yoma token from SSO. | `.env`, host page | Yoma stage org admin and SSO client |
| 3. GenU Azure | Set `AI_PROVIDER=foundry` and `CONTENT_SAFETY_PROVIDER=azure` with the Azure keys. No code changes. | `.env` | Azure OpenAI and Content Safety resources |
| 4. Production store | Swap the in-memory flags and ratings in `lib/store.ts` for a database with a 72-hour TTL. Same function signatures. | `lib/store.ts` | A database |
| 5. Inside Yoma | Embed `/embed/skilllens` in a Yoma page the same way as P2E: set `NEXT_PUBLIC_P2E_ORIGIN` to Yoma's origin, which sets both `frame-ancestors` and the `postMessage` target. To keep P2E and Yoma at once, make that setting a list. Or move the components into Yoma's web app. The API routes stay as they are. | `next.config.ts`, `components/*` | Yoma front-end team |
| 6. Partner endpoint | If Yoma adds the endpoint from open question 2, replace `submitToYoma` in `lib/yoma/client.ts`. Its callers keep the same `YomaResult`. | `lib/yoma/client.ts` | Yoma API change |

### Seams that make this possible

- **AI:** `lib/ai/provider.ts` picks mock, local or Azure (`foundry`) from `AI_PROVIDER`. All three share one OpenAI-compatible client and the same versioned prompts (`prompts/v2`).
- **Safety:** `lib/safety/contentSafety.ts` picks stub, model or Azure from `CONTENT_SAFETY_PROVIDER`. The rest of the pipeline (`lib/pipeline.ts`) does not change.
- **Yoma:** all Yoma calls are in `lib/yoma/`. Field names follow Yoma's C# request models, and every call has a mock or snapshot fallback.
- **Data contract:** the saved practice sample (section 2) is a Zod schema in `lib/schema.ts`. It is the one shape P2E, Yoma and SkillLens all share.
- **Look:** every colour and font is a token in `app/globals.css` (`@theme`). Today they match the Passport to Earning site: blue `#0a59a7`, yellow `#ffcb03`, Oswald and Jost. A Yoma-branded version means changing that one block.
- **Status:** `/status` and `GET /api/status` show which integrations are real or simulated, and whether each one can be reached.

## 7. Where SkillLens meets Yoma's code, and what a live GenU environment needs

### Yoma files SkillLens depends on

Paths are in the Yoma repository ([Devin-Fitzpatrick06/yoma](https://github.com/Devin-Fitzpatrick06/yoma)). If any of these change, update `lib/yoma/` to match.

| Yoma file | What SkillLens uses | SkillLens file |
| --- | --- | --- |
| `src/api/src/application/Yoma.Core.Api/Controllers/MyOpportunityController.cs` | `PUT action/{opportunityId}/verify`, role `User` (the learner's own token) | `lib/yoma/client.ts` |
| `src/api/src/domain/Yoma.Core.Domain/MyOpportunity/Models/MyOpportunityRequestVerify.cs` | Multipart field names (`Certificate`, `DateStart`, `DateEnd`, `Feedback`) | `lib/yoma/evidence.ts` |
| `src/api/src/application/Yoma.Core.Api/Controllers/OpportunityController.cs` | Public `POST opportunity/search` | `lib/yoma/opportunities.ts` |
| `src/api/src/domain/Yoma.Core.Domain/Opportunity/Models/OpportunitySearchFilterBase.cs` | Search filters (no skills filter today) | `data/yoma/opportunity-search.json` |
| `src/api/src/application/Yoma.Core.Api/Controllers/LookupController.cs` | Public `GET lookup/skill` | `scripts/yoma-sync.ts` |
| `docs/sso/README.md` | How a host page gets the learner's Yoma token | host page (P2E or Yoma) |

### What works today, with no accounts

- **Real:**
  - Skills lookup and opportunity search against Yoma stage.
  - The local AI model (Ollama) and the model-based safety check.
- **Built, but preview only:**
  - The verify request. `YOMA_MODE=mock` builds the exact multipart request, then shows it instead of sending it.
- **Simulated:**
  - Azure AI and Azure Content Safety. The code path exists, but needs keys.

`/status` shows which is which at any moment.

### Needed for a live GenU environment

| Kind | Item | Who provides it |
| --- | --- | --- |
| API | Azure OpenAI (Foundry) deployment, text and vision | GenU Azure tenant |
| API | Azure AI Content Safety resource | GenU Azure tenant |
| API | Yoma production API base URL (`YOMA_API_BASE_URL`) | Yoma team |
| API (optional) | A Yoma partner endpoint for practice samples (section 3, open question 2) | Yoma team |
| Permission | An organisation-admin account on Yoma, to create the two practice opportunities with FileUpload verification and attach skills | Yoma / GenU |
| Permission | A Yoma SSO (Keycloak) client for the host page, so it can pass the learner's token with role `User` | Yoma team |
| Permission | The host's Content-Security-Policy allows SkillLens in a frame (`frame-src`); SkillLens allows the host in `frame-ancestors` via `NEXT_PUBLIC_P2E_ORIGIN` | P2E / Yoma front-end |
| Permission | Facilitator sign-in through P2E or Yoma, replacing the shared password | P2E / Yoma |
| Infrastructure | Node 24 hosting, for example Azure App Service or a container on Yoma's Kubernetes (`helm/`) | GenU / Yoma |
| Infrastructure | Secrets in a vault (for example Azure Key Vault), never in the repo | GenU |
| Infrastructure | A database with a 72-hour TTL for flags and ratings (`lib/store.ts`) | GenU |
| Infrastructure | Rate limiting on `/api/*`, plus logging that holds flag ids only | GenU |
| Review | Child-safeguarding and data-protection sign-off (section 4), and native-speaker review of the Pidgin text | UNICEF |
