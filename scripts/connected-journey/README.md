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
```

| App | Address | Started as |
| --- | --- | --- |
| PathFinder | `http://localhost:4173/` | `node serve.mjs`, `PORT=4173`, `NEXT_PROTOTYPE_URL=http://localhost:3001/demo/p2e` |
| SkillLens | `http://localhost:3001/demo/p2e` | `npm run dev -- -p 3001`, `NEXT_PUBLIC_YOCV_URL=http://localhost:4174` |
| YoCV | `http://localhost:4174/` | `node server.js`, `PORT=4174` |

- Logs and process ids go to `scripts/connected-journey/.logs/` (git-ignored).
- The first time, SkillLens gets `npm ci` and its own `npm run setup`, which writes its `.env.local` in that clone
  (simulated AI unless Ollama has its model, and a random signing secret). Nothing is written to this repository, and no
  secret is printed or needed.
- `ZIQI_URL` (default `http://localhost:3000`) only changes the listing address the script prints.

## The click path

1. **Ziqi:** sign in as `halima.yusuf@example.org` / `P@ssword12` (local only, fictional). My Yoma shows her route on
   Yoma, the grades and the scam job held back. Open the solar job,
   `http://localhost:3000/opportunities/c0a7e000-0000-4000-8000-000000000301`, and Save it.
2. **"Plan it with PathFinder"** opens PathFinder: the plan.
3. **"Continue to SkillLens"** opens SkillLens: the challenge, passed on attempt 2, credential recorded.
4. **"Continue to YoCV"** opens YoCV: the CV with the verified credential, the QR, Apply.
5. **"Back to the listing"** returns to the Ziqi listing, which shows "Job Application received", with the thread
   complete.

Every link opens in the same tab.
