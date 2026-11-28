# Connected journey launcher

Starts PathFinder, SkillLens and YoCV on this machine, with the addresses the journey contract sets, so the four BBS
prototypes run as one journey: Ziqi → PathFinder → SkillLens → YoCV → back to the Ziqi listing. Everything they show
is the fictional shared mock data v1.1 (Halima Yusuf and the solar job), and every screen says mock-up or sample data.

Ziqi (Phase 1) is not started here. Start it first, with its journey listings, Halima's sign-in and its journey switches:

```bash
scripts/course-quality-demo/start.sh --journey
```

The plan and the contract: `docs/work/active/course-quality-judger/connected-journey.md` and `journey-contract.md`.

## What you need

- Node 24 (SkillLens asks for it; PathFinder and YoCV run on it too).
- A local clone of each of the three prototypes, with its connected-journey patch applied (below).
- Ports 4173, 3001 and 4174 free. Ziqi uses 3000.

## Patches

Each clone needs its `connected-journey` patch. The patches and their notes are in
`docs/work/active/course-quality-judger/connected-journey/patches/<app>/`, one folder per app. Apply them with `git am`,
as each folder's README says, for example:

```bash
cd ~/code/skilllens
git checkout -b connected-journey main
git am /path/to/yoma/docs/work/active/course-quality-judger/connected-journey/patches/skilllens/*.patch
```

The script warns, and carries on, when a clone looks unpatched: the app then runs, but shows no journey cards and links
nowhere.

## Run it

```bash
PATHFINDER_DIR=~/code/pathfinder-prototype \
SKILLLENS_DIR=~/code/skilllens \
YOCV_DIR=~/code/yocv-prototype \
  scripts/connected-journey/journey.sh

scripts/connected-journey/journey.sh status   # which of the three are running
scripts/connected-journey/journey.sh stop     # stops only what this script started
scripts/connected-journey/journey.sh reset    # un-saves Halima's solar job, so the demo can run again
```

| App | Address | Started as |
| --- | --- | --- |
| PathFinder | `http://localhost:4173/` | `node serve.mjs`, `PORT=4173`, `NEXT_PROTOTYPE_URL=http://localhost:3001/demo/p2e` |
| SkillLens | `http://localhost:3001/demo/p2e` | `npm run dev -- -p 3001`, `NEXT_PUBLIC_YOCV_URL=http://localhost:4174`, and the four settings below |
| YoCV | `http://localhost:4174/` | `node server.js`, `PORT=4174` |

- Logs and process ids go to `scripts/connected-journey/.logs/` (git-ignored).
- The first time, SkillLens gets `npm ci` and its own `npm run setup`, which writes its `.env.local` in that clone.
  Nothing is written to this repository, and no secret is printed or needed.
- `ZIQI_URL` (default `http://localhost:3000`) only changes the listing address the script prints.

### SkillLens: deterministic and offline

The script starts SkillLens with these, so every rehearsal shows the same thing and needs no network. Each is used only
when it is not already set in the shell that runs the script, and only SkillLens gets them (they override its
`.env.local`):

| Setting | Value | Why |
| --- | --- | --- |
| `AI_PROVIDER` | `mock` | The scripted AI: the same challenge and the pass on attempt 2, every time. |
| `CONTENT_SAFETY_PROVIDER` | `stub` | No call to an outside safety service. |
| `YOMA_OPPORTUNITIES` | `snapshot` | SkillLens's bundled opportunities, not a live Yoma. |
| `PASS_SIGNING_SECRET` | random, made once | Generated the first time into `.logs/skilllens-pass-signing-secret` (git-ignored, owner-only, never printed) and reused on every start, so a restart keeps the passes it signed valid. Delete the file to get a new one. |

### Running the demo again

Saving the solar job is step 1, so a second run (a rehearsal, or a Playwright run) needs it un-saved first:

```bash
scripts/connected-journey/journey.sh reset
```

It removes Halima's save of `opp-job-001` from the Ziqi demo database, with the same `DELETE` that
`scripts/course-quality-demo/seed/generate-journey-sql.mjs` runs on every `start.sh --journey`, through
`docker exec … psql` on `yoma-course-quality-demo-postgres-1` (`DEMO_POSTGRES` to name another container). Nothing else
changes: Halima's My Yoma answers stay, and the other three apps keep no state between runs (reload them). Reload the
listing afterwards.

## The click path

1. **Ziqi:** sign in as `halima.yusuf@example.org` / `P@ssword12` (local only, fictional). My Yoma shows her route on
   Yoma. Open the solar job,
   `http://localhost:3000/opportunities/c0a7e000-0000-4000-8000-000000000301`: it shows Ziqi's verdict, "Checked by
   Ziqi: Verified · Grade A". Save it.
2. **"Continue to PathFinder"** opens PathFinder: the plan, with the scam job held back from it.
3. **"Continue to SkillLens"** opens SkillLens: the challenge, passed on attempt 2, credential recorded.
4. **"Continue to YoCV"** opens YoCV: the CV with the verified credential, the QR, Apply.
5. **"Back to the listing"** returns to the Ziqi listing, which shows "Job Application received", with the thread
   complete.

Every link opens in the same tab.

Presenter notes:

- **My Yoma** asks 4 quick questions on Halima's first visit (she has no My Yoma profile; that is deliberate, it shows
  My Yoma). Pick **Sales & customer service**.
- **SkillLens:** switch to **Computer** view if it opens in Phone view. Tap **"Save to my profile"** before
  **"Add Skill Credential to my YoID"**.
- **YoCV:** pick the role **"Solar installation assistant"** if it is not preselected.
- The verdict line on the solar job needs `start.sh --journey`, which lets the API return a job's public card. Without
  it the line is hidden, never guessed.
