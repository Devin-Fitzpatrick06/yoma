# Setup

Two ways to run Ziqi: the **local demo** (one command, everything in Docker, recommended for showing it) and a
**development setup** (API and website on your machine, for changing code).

## 1. Local demo (Mac or Linux, about 5–8 minutes)

Needs Docker Desktop running with about 6 GB of memory, and the ports 80, 3000, 5050 and 8080 free. No .NET or Node is
needed on the machine.

```bash
git clone https://github.com/Devin-Fitzpatrick06/yoma.git
```

```bash
cd yoma && git checkout feature/course-quality-judger
```

```bash
scripts/course-quality-demo/start.sh
```

What a start does depends on whether a saved demo database exists in `scripts/course-quality-demo/.cache/`
(git-ignored, never committed):

| Start | What happens | Time |
| --- | --- | --- |
| Saved database present | Loads it; adds live impact actions, events and jobs if it has none; re-checks the 8 named demo courses | about 3–5 minutes |
| No saved database (default) | Imports every live Yoma course, 2,000 live jobs, the live impact actions and the live events from the public Yoma API, checks and grades a sample of 40 courses, opens the site | about 8 minutes |
| `--full` | As above, but checks and grades every live course (about 2,400) and saves the database for fast later starts | about 45 minutes |
| `--fixtures` | Uses only the local demo pages for the 8 named courses (for unreliable internet) | a few minutes |
| `--local-ai` (with any of the above) | Also turns on Ziqi's AI check with a free open model in Docker (see "Ziqi AI" below) | adds the first model download, about 2 GB, during the build |

When it prints "Course Quality demo is running", open `http://localhost:3000`. Local test sign-ins (public seed
defaults from `src/api/docker-compose.yml`, local only), password `P@ssword12`:

| Account | Sees |
| --- | --- |
| `testuser@gmail.com` | Learner: My Yoma (first visit lands there), YoID "My path" card |
| `testorgadminuser@gmail.com` | Partner: the Ziqi check panel when creating a listing, the partner badge |
| `testadminuser@gmail.com` | Admin and reviewer: Ziqi reviewer queue, rubric editor, pathway health; also a partner of the demo organisation |

Stop and remove the demo (the saved database in `.cache/` stays):

```bash
scripts/course-quality-demo/stop.sh
```

### Ziqi AI in the demo: a free, local open model

The AI is off unless you ask for it. The start-up checks always run without it; with an AI option the API restarts with
the AI on at the end of the start, and a reviewer runs it on demand (**Run AI check** on a reviewer card, or
`scripts/course-quality-demo/ai-smoke.sh` from the terminal, which prints the model, tokens, cost, duration and
outcome).

```bash
scripts/course-quality-demo/start.sh --local-ai
```

| Option | What it does |
| --- | --- |
| `--local-ai` | Runs Ollama (`ollama/ollama:0.12.3`) in the demo with `qwen2.5:3b`; downloads it once into the Docker volume `yoma-course-quality-demo-ollama-models` (kept by `stop.sh`). Internal only: no port is published. |
| `--local-ai-model NAME` | Another Ollama model, e.g. `qwen2.5:1.5b` (about twice as fast, weaker answers). |
| `--local-ai-url URL` | The native Ollama app instead of the container: `--local-ai-url http://host.docker.internal:11434/v1`. |
| `--ai-url URL --ai-model NAME` | Any OpenAI-compatible server, e.g. a government-hosted model. A key, if needed, comes from the environment variable `ZIQI_AI_API_KEY` and is never written to a file. |

On a Mac, Docker Desktop has **no GPU access**, so the container runs the model on the CPU: on a 4-core CPU with nothing
else running, 58 to 98 seconds per course check with `qwen2.5:3b` (measured 28 November 2026 on Linux; not yet timed on
a Mac, `ai-smoke.sh` prints the real time). While the machine was busy a check took up to 4 min 50 s, close to the
300-second limit, so keep it otherwise idle while presenting or use `qwen2.5:1.5b` (faster, weaker). Give
Docker about 8 GB of memory. The native Ollama app uses the Mac's GPU through Metal and is much faster: install it from
ollama.com, run `ollama pull qwen2.5:3b`, and start the demo with `--local-ai-url http://host.docker.internal:11434/v1`.

### Link health, on any platform

- In the website: `http://localhost:3000/admin/course-quality/links` (reviewer sign-in). Paste or upload links, run,
  export a CSV.
- Without Yoma: `node scripts/link-health/check-links.mjs` reads a .txt, .csv or sitemap and writes a CSV or Markdown
  report. It needs only Node 24 and no packages; see `scripts/link-health/` for its options.
- The API endpoint behind the page: `POST /courseQuality/links/check` (Admin), at most 200 links per request. One request
  spends at most `CourseQuality:LinkHealth:BudgetInSeconds` (default 240) starting checks; links not started by then come
  back as `Timeout` with "Not checked: the check ran out of time. Run these links again."

Click-by-click script: `docs/work/active/course-quality-judger/demo-walkthrough.md`. More detail:
`scripts/course-quality-demo/README.md`.

## 2. Development setup

Toolchain (pinned in `mise.toml`): .NET 10, Node 24, pnpm 11. From the repository root:

```bash
mise install
```

```bash
pnpm install --frozen-lockfile
```

Infrastructure (Postgres with PostGIS, Valkey, Keycloak) uses Yoma's own compose file: `src/api/docker-compose.yml`.
Then run the API from `src/api/src/application/Yoma.Core.Api` and the website from `src/web` (`pnpm dev`) as described
in `src/api/AGENTS.md` and `src/web/AGENTS.md`. The demo's `scripts/course-quality-demo/demo.env` shows every
setting the demo uses and is the easiest reference for a working local configuration.

### Switches (all off by default)

| Setting | Where | Turns on |
| --- | --- | --- |
| `CourseQuality:Enabled` | API | Ziqi's checks (assessment, reviewer queue, learner card) |
| `CourseQuality:ShowInBrowse` | API | Ziqi status and ranking in opportunity search |
| `CourseQuality:VerifyTypes` | API | Listing checks for more types: `Event`, `ImpactAction` (Learning is always on) |
| `CourseQuality:Jobs:Enabled` | API | Job checks and A–D job grades |
| `CourseQuality:LearnerFeedback:Enabled` | API | "Did you take it?" feedback |
| `CourseQuality:PrePublish:Enabled` | API | The partner draft check endpoint |
| `CourseQuality:PrePublish:UseAI` | API | The configured AI (`AI:Provider`) in the partner draft check. Off: the draft check reads the page with the keyless reader only. Keep it off with a CPU-only local model, which takes minutes while the form waits 45 s, and note that a draft check's AI usage is not recorded |
| `Pathway:Enabled` | API | Every `/pathway` endpoint (My Yoma, badges, outcomes, steps); 404 when off |
| `Pathway:DropOffAvailable` | API | Showing drop-off once the pilot has data (keep off until then) |
| `NEXT_PUBLIC_PATHWAY_ENABLED` | Web (build time) | My Yoma tab, first-visit redirect, YoID card, badge chips, pathway health |
| `NEXT_PUBLIC_PREPUBLISH_ENABLED` | Web (build time) | The Ziqi check panel on the partner listing form |
| `NEXT_PUBLIC_COURSE_QUALITY_DEMO` | Web (build time) | Demo-only: view switcher and fixture preview pages |
| `NEXT_PUBLIC_JOURNEY_ENABLED` | Web (build time) | Demo-only: the connected four-prototype journey (Ziqi → PathFinder → SkillLens → YoCV): the handoff card and YoID thread on the journey listing, the YoID journey card, and "Your route on Yoma" with the PathFinder card on My Yoma. Off unless `true` |
| `NEXT_PUBLIC_JOURNEY_LISTING_ID` | Web (build time) | The Yoma id of the journey listing (`opp-job-001`): `c0a7e000-0000-4000-8000-000000000301` in the local demo. No journey UI without it |
| `NEXT_PUBLIC_JOURNEY_YOID` | Web (build time) | The mock YoID on the thread and the PathFinder link. Default `yoid-ng-7f3a0123` |
| `NEXT_PUBLIC_PATHFINDER_URL` | Web (build time) | PathFinder's address for "Continue to PathFinder". Default `http://localhost:4173`; only `http:`/`https:` |

Keep the web and API pathway switches on together.

### AI configuration (iteration 5)

| Setting | Meaning |
| --- | --- |
| `AI:Provider` | `None`, `Anthropic` or `OpenAICompatible`. Default `Anthropic`, which without a key means no AI. |
| `AI:OpenAICompatible:BaseUrl` | The server's OpenAI-compatible address, e.g. `http://localhost:11434/v1` (Ollama), vLLM, LM Studio or a government endpoint. Ziqi posts to `<BaseUrl>/chat/completions`; a query in the base URL (e.g. `?api-version=...`) is kept after that path. |
| `AI:OpenAICompatible:Model` | e.g. `qwen2.5:3b`. |
| `AI:OpenAICompatible:ApiKey` | Optional. Environment or secret store only, never a file in git. |
| `AI:OpenAICompatible:IsLocal` | Default `false`. Set `true` only for a model on the operator's own host: each run's cost is then recorded as 0. Left `false`, a remote run's cost comes from `AI:Prices`, or is recorded as unknown. |
| `AI:OpenAICompatible:TimeoutInSeconds`, `MaxTokens`, `MaxPageChars` | Limits per call. The demo uses a 300-second timeout for CPU models. |
| `AI:Prices:<model>:InputPerMillionUsd`, `OutputPerMillionUsd` | Prices for a remote model. Unknown model: the cost is recorded as unknown, never guessed. |
| `Anthropic:*` | Unchanged: `ApiKey`, `Model`, `Effort`, `MaxTokens`, `TimeoutInSeconds`, `MaxRetries`. |

Whichever provider runs, Ziqi sends the same system prompt and output schema (`prompts/`), wraps page text as untrusted
data, and lets the AI only lower a result. Each run's provider, model, tokens, cost, duration and outcome are stored
with the assessment (`aiUsage`) and summed at `GET /courseQuality/ai/usage` (Admin).

As environment variables, `:` becomes `__`, e.g. `AI__Provider=OpenAICompatible`,
`AI__OpenAICompatible__BaseUrl=http://localhost:11434/v1`. A development API on your machine can use a native Ollama
directly: `ollama pull qwen2.5:3b`, then set those two and `AI__OpenAICompatible__Model=qwen2.5:3b`,
`AI__OpenAICompatible__IsLocal=true`.

### Keys and secrets

- **Nothing secret is committed.** `Anthropic:ApiKey` is empty in `appsettings.json`; with no key, Ziqi runs its
  keyless page reader and the AI is simply not used.
- **A local open model needs no key** and costs nothing (see "Ziqi AI in the demo" above).
- A paid or hosted model's key goes only through the environment, for example `Anthropic__ApiKey` or
  `AI__OpenAICompatible__ApiKey` (the demo reads it from `ZIQI_AI_API_KEY` at start and passes it to the API container
  only at runtime), or your deployment's secret store (Yoma's deployments keep secrets SOPS-encrypted under
  `helm/**/secrets.yaml`). Never in a file in git.
- `scripts/course-quality-demo/demo.env` holds only local placeholders and the public defaults already in Yoma's
  `src/api/docker-compose.yml` (local Keycloak client secrets, local database password). They work only against the
  local containers. `NEXTAUTH_SECRET` is generated fresh on every demo start.

## 3. Tests

API (from `src/api`):

```bash
dotnet build Yoma.Core.sln
```

```bash
dotnet run --project src/test/Yoma.Core.Test -- -namespace "Yoma.Core.Test.CourseQuality*" -namespace "Yoma.Core.Test.Pathway*" -namespace "Yoma.Core.Test.Handover*"
```

`dotnet test` does not work with this test project on .NET 10; use `dotnet run` as above. Opt-in tests (live web pages,
a real Postgres) are skipped unless their environment variables are set; see `test-cases/README.md`.

Web (from `src/web`):

```bash
pnpm test
```

```bash
pnpm lint
```

To refresh the prompt and rubric files in this folder after changing a default on purpose:

```bash
ZIQI_HANDOVER_UPDATE=1 dotnet run --project src/test/Yoma.Core.Test -- -namespace "Yoma.Core.Test.Handover*"
```
