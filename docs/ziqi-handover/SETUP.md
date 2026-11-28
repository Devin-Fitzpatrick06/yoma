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
| Saved database present | Loads it; adds live impact actions and jobs if it has none; re-checks the 8 named demo courses | about 3–5 minutes |
| No saved database (default) | Imports every live Yoma course, 2,000 live jobs and the live impact actions from the public Yoma API, checks and grades a sample of 40 courses, opens the site | about 8 minutes |
| `--full` | As above, but checks and grades every live course (about 2,400) and saves the database for fast later starts | about 45 minutes |
| `--fixtures` | Uses only the local demo pages for the 8 named courses (for unreliable internet) | a few minutes |

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
| `Pathway:Enabled` | API | Every `/pathway` endpoint (My Yoma, badges, outcomes, steps); 404 when off |
| `Pathway:DropOffAvailable` | API | Showing drop-off once the pilot has data (keep off until then) |
| `NEXT_PUBLIC_PATHWAY_ENABLED` | Web (build time) | My Yoma tab, first-visit redirect, YoID card, badge chips, pathway health |
| `NEXT_PUBLIC_PREPUBLISH_ENABLED` | Web (build time) | The Ziqi check panel on the partner listing form |
| `NEXT_PUBLIC_COURSE_QUALITY_DEMO` | Web (build time) | Demo-only: view switcher and fixture preview pages |

Keep the web and API pathway switches on together.

### Keys and secrets

- **Nothing secret is committed.** `Anthropic:ApiKey` is empty in `appsettings.json`; with no key, Ziqi runs its
  keyless page reader and the AI is simply not used.
- To turn the AI on, set the key only through the environment, for example `Anthropic__ApiKey`, or your deployment's
  secret store (Yoma's deployments keep secrets SOPS-encrypted under `helm/**/secrets.yaml`). Never in a file in git.
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
