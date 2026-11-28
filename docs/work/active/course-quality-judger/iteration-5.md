# Ziqi iteration 5: live AI end to end, swappable model, wider audits, link health

Status: in-progress

Source: the BBS "Ziqi (Jongmin), priority 1" asks, 2026-11-28. Lead rule: **no paid AI** — everything is built,
improved and verified on a free, locally hosted open model (Ollama, `qwen2.5:3b`); the Anthropic path stays and is
tested with fakes, never billed.

| Ask | Workstream |
| --- | --- |
| Run a live LLM through the Yoma backend, show the result in the Yoma front end, with token usage and cost per run | **E1** usage + cost capture and storage, **E3** reviewer card + admin panel |
| Model swappable through configuration; shown running on a locally hosted open model | **E1** provider switch + OpenAI-compatible provider, **E4** Ollama in the demo |
| Extend audits beyond courses to events, impact actions and jobs | Built (iterations 3–4); **E4** imports live events so all four types are audited in the demo |
| Link health check that works on any platform (Yoma, YouthHub, 1MiO) | **E2** endpoint + standalone CLI, **E3** admin page |
| If time allows: partner badges, learner outcomes in scoring | Built (iteration 4); verify only |

## Shared contract

`src/web/src/api/models/courseQuality.ts`, block "Iteration 5" (CourseQualityAIUsage, CourseQualityAIUsageSummary,
LinkHealth*), and `CourseQualityAssessment.aiUsage`. Endpoints:

- `GET  /courseQuality/ai/usage` (Admin) → CourseQualityAIUsageSummary
- `POST /courseQuality/links/check` (Admin) body LinkHealthRequest → LinkHealthReport
- existing `POST /courseQuality/opportunity/{id}/assess` (Admin) now records `aiUsage` when an AI ran

## Configuration (API)

```
AI:Provider = None | Anthropic | OpenAICompatible      (default: Anthropic, which is keyless — no AI — without a key)
AI:OpenAICompatible:BaseUrl  e.g. http://localhost:11434/v1   (Ollama, vLLM, LM Studio, a government endpoint)
AI:OpenAICompatible:Model    e.g. qwen2.5:3b
AI:OpenAICompatible:ApiKey   optional, environment only
AI:OpenAICompatible:IsLocal  true → cost 0
AI:OpenAICompatible:TimeoutInSeconds, MaxTokens, MaxPageChars
AI:Prices:<model>:InputPerMillionUsd / OutputPerMillionUsd   (remote models; unknown → cost null, never guessed)
```

The existing `Anthropic` section is unchanged. Whatever the provider, the safety design is the same: the same system
prompt and output schema, untrusted-data delimiters, and the rule that the AI can only lower a result.

## Engineers

| Engineer | Owns | Builds |
| --- | --- | --- |
| **E1 AI platform** | `Yoma.Core.Infrastructure.Anthropic/**`, a new `Yoma.Core.Infrastructure.OpenAICompatible` project, AI options and usage models in `CourseQuality/`, the one migration `ApplicationDb_CourseQuality_AIUsage`, the usage endpoint | Provider switch; OpenAI-compatible provider (HttpClient, no new NuGet); usage + cost capture on every AI call (both providers); persistence on the assessment; usage summary; tests with fakes |
| **E2 Link health** | new `CourseQuality/Services/LinkHealth*`, its endpoint, `scripts/link-health/**` | Endpoint reusing the safe link checker (≤200 URLs, per-host pacing, timeouts); standalone Node CLI (no deps) that reads a .txt/.csv/sitemap and writes a CSV/Markdown report, usable on any platform without Yoma; tests |
| **E3 Web** | `src/web` reviewer page AI section, new `components/CourseQuality/AIUsage*`, new `pages/admin/course-quality/links.tsx`, admin index panel | Reviewer card: model, provider, local/free badge, tokens in/out, cost, duration, outcome, "Run AI check" button; admin "AI usage" panel; link health page (paste or upload, run, results, export CSV) |
| **E4 Demo & data** | `scripts/course-quality-demo/**`, `docs/ziqi-handover/**` | Ollama service + model pull in the demo (`--local-ai`), AI settings in demo.env, live events import, `start.sh` links, handover docs updated |

Merge order: E1 → E2 → E4 → E3, then an integrated run on the local model, a review and a test pass.
