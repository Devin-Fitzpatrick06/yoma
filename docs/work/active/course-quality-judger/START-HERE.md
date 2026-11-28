# Start here: context for a new session (as of 2026-11-28)

Paste this file, or its link, into a new Claude Code chat to pick up the project. It is a map, not a history. The
detail lives in the files it links to, and the code wins wherever the two disagree.

## The project in five lines

- **What:** Ziqi, a quality auditor for Yoma's opportunities, built by Jongmin (GitHub `jchae7`) as part of a Berkeley
  Business Society pro bono project for Yoma and GenU.
- **What it does:** checks every published listing (courses, jobs, events, impact actions), grades courses and jobs
  A–D, awards partner badges, checks partner drafts, and builds each youth a route on Yoma ("My Yoma").
- **The wider deliverable:** four prototypes forming one journey for a fictional youth, Halima:
  1. Ziqi: Saved Opportunity
  2. PathFinder: Learning Plan
  3. SkillLens: Skill Credential
  4. YoCV: Job Application, then back to the Ziqi listing
- **The split:** Ziqi maps within Yoma; PathFinder maps across platforms (Yoma, Passport to Earning, WhatsApp/SMS).
- **Status:** built and verified end to end (62/62 browser checks). Not yet rehearsed on a Mac. Teammates' pull
  requests are open.

## Repositories and links

| What | Where |
| --- | --- |
| Ziqi code (working repo) | `Devin-Fitzpatrick06/yoma`, branch `feature/course-quality-judger`; mirrored to `jchae7/ziqi-yoma` (same branch). Push to both. |
| All four connected | https://github.com/jchae7/genu-connected-journey: PathFinder, SkillLens and YoCV as subtrees with history; Ziqi as a submodule; `journey.sh` |
| Teammates' forks | `jchae7/pathfinder-prototype`, `jchae7/skilllens`, `jchae7/yocv-prototype`, branch `connected-journey`; pull requests are open to the owners' repos |
| Owners | PathFinder: Anagha (`anaghataneja-stack`). SkillLens: Charisse (`wongkycharisse`). YoCV: `greenglow7`. Yoma: Adrian (API), Jason (web). |

## Read first, in this order

1. `docs/ziqi-handover/README.md`: what Ziqi is, where the code lives, and its status. Also in that folder:
   - `SETUP.md`
   - `LIVE-RULES-MOCKED.md`
   - `LIMITATIONS-AND-NEXT-STEPS.md`
   - `prompts/`, `rubrics/`, `test-cases/`
2. `docs/work/active/course-quality-judger/`:
   - `connected-journey.md`: the four-app plan and click path
   - `journey-contract.md`: the exact links, card wording and id mapping
   - `demo-walkthrough.md`: the Ziqi-only demo script
   - `iteration-5.md`: AI on a free local model, link health, audits for all four types
3. The latest handoffs in `docs/work/active/course-quality-judger/handoffs/`:
   - `2026-11-28-i5.md`: iteration 5
   - `2026-11-28-j1.md` and `2026-11-28-j2.md`: the connected journey (data, website, fix round)
   - `2026-11-28-fixes.md`: the review fixes
4. `AGENTS.md` (repo root) and `src/api/AGENTS.md`, `src/web/AGENTS.md`: conventions. Follow them.

## Rules that bind

- **No paid AI.** Use only the free local model (Ollama, `qwen2.5:3b`, `AI:Provider=OpenAICompatible`). Anthropic stays
  supported but is never called for real.
- **Repo conventions (AGENTS.md):**
  - Write a handoff in `handoffs/` every session.
  - Every commit SHA cited in a doc must resolve (`git cat-file -e`).
  - Conventional commits.
  - Don't touch `helm/`, `.github/` or `env.secrets`.
  - The agent roles in `.claude/agents/` are opt-in only.
- **Fiction and data:** the shared mock data (Halima, `opp-job-001`, …) is fictional. Use those names and ids exactly;
  partner screens show counts, never people.
- **Ownership:** don't push to the teammates' repositories; go through the `jchae7` forks and pull requests. API changes
  need Adrian's review; visible web changes need Jason's.
- **Confidentiality:** don't share planning material with Camila Gloria until Jongmin says so.
- **How Jongmin wants answers:** advisor tone. Lead with the uncomfortable truth, tag claims [certain], [likely] or
  [guessing], no filler phrases, and hold a position unless given new information. Prefer the real Yoma site over
  mock-up pages.

## Run it

- **Ziqi only:** run `scripts/course-quality-demo/start.sh`, then open `http://localhost:3000`. Add `--local-ai` for the
  AI check, or `--journey` for the four-app journey data and Halima's sign-in (`halima.yusuf@example.org` /
  `P@ssword12`).
- **All four:** run `ziqi/scripts/course-quality-demo/start.sh --journey` in `genu-connected-journey`, then
  `./journey.sh`. The click path is in that repo's README. `./journey.sh reset` un-saves the job so the demo can be
  rerun.
- **Tests:**
  - API: `dotnet run --project src/test/Yoma.Core.Test -- -namespace "Yoma.Core.Test.CourseQuality*"` (and `Pathway*`,
    `Handover*`). Not `dotnet test`.
  - Web: `pnpm test`, `pnpm lint`, `npx tsc --noEmit`.

## Known gaps and open items

- **Not rehearsed on a Mac.** The cloud sandbox ran the API and website natively, and Node 22 instead of 24. In that
  sandbox the demo's fixture pages need a routable Docker network; see `handoffs/2026-11-28-i5.md`, Gotchas.
- **CPU speed:** a local-model check takes 58 to 98 s when the machine is idle, and up to about 5 minutes when it is
  busy.
- **The small model is cautious:** it can lower a course to "Needs review".
- **Demo-only setting:** `start.sh --journey` makes job verdicts public (`CourseQuality:Jobs:ShowInBrowse`) so the
  solar job shows "Verified · Grade A".
- **My Yoma has no solar goal.** Halima answers 4 questions on her first visit; pick "Sales & customer service".
- **Waiting on others:** teammates' pull requests not yet merged; Yoma owner reviews; the 100-course validation sample
  is not yet rated.
- **Next aspect:** not yet defined. Ask Jongmin for the brief.
