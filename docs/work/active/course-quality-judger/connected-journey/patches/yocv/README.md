# YoCV patch: the connected journey (Phase 4)

For the YoCV owners (`greenglow7/yocv-prototype`). Three commits on a local branch `connected-journey`, cut from
`main` at `7de5915`. They implement YoCV's part of journey contract v1
(`docs/work/active/course-quality-judger/journey-contract.md` in the Ziqi/Yoma repository). Nothing was pushed.

| Patch | What it does |
| --- | --- |
| `0001-feat-yocv-default-port-4174-…` | Default port 4173 → **4174** (PathFinder keeps 4173). `PORT` still overrides. Run guides, handover pack, demo script, `.env.example`, `CLAUDE.md` and `AGENTS.md` updated. |
| `0002-feat-yocv-connected-journey-…` | The journey: link params, arriving card, YoID thread, leaving card with the link back to Ziqi. |
| `0003-docs-yocv-…` | Spec `docs/superpowers/specs/2026-11-28-yocv-connected-journey-design.md`, README paragraph and "Real versus simulated" row, Phase 4 slide line, testing-log entry. |

## What changes for the user

Only when the page is opened with `journey=demo`:

- SkillLens's link `/?as=halima&journey=demo&from=skilllens&yoid=…&listing=…&goal=…&plan=…&course=…&credential=cred-002&return=…`
  signs Halima in (as before). The nine contract params leave the address the way `?as=` does, and stay in
  `sessionStorage` (`yocv.journey`) for the tab. Arriving with no route opens `#/cv`.
- Step 1 starts with the card **From SkillLens · Phase 3 / Skill Credential / Solar PV Installation Basics: practical
  challenge passed / ✓ Recorded on your YoID (mock-up)**. `cred-002` keeps its tag **Skill Credential, from SkillLens**.
- Every CV step shows the thread `YoID · yoid-ng-7f3a0123 · mock-up`: three steps recorded, Job Application in progress,
  then recorded after Apply.
- After **Send my application** records `app-001`: the card **Back to Ziqi · Phase 1 / Job Application / Application to
  Solar Installation Assistant (Trainee)** with the button **Back to the listing**, linking in the same tab to
  `<return>?journey=demo&from=yocv&yoid=yoid-ng-7f3a0123&application=app-001` (`&` if `return` already has a query).
  A `return` that is not `http:`/`https:` hides the button.

Without `journey=demo` the page behaves exactly as before.

## Where in the code

- `core.js`: `JOURNEY_KEYS`, `journeyParams`, `journeyReturn`, `journeyBackHref`, `journeyThread` (pure, unit-tested).
- `yoid.js`: `resolveJourney()` at boot; passes `journey` to `YoCVApp.mount`; `YoID.journey()`.
- `app.js`: `arrivingHTML`, `threadHTML`, `leavingHTML`.
- `data/journey.json`: new `connected` block (existing keys untouched). `cv-copy.js`: the contract wording.
- `styles.css`: `.journey-card`, `.journey-thread`; new token `--blue:#4cade9`.

Two house tests were changed on purpose, please check you agree: `tests/restyle.test.cjs` lists the new `--blue` token,
and `tests/yoid-shell.test.cjs` now allows a second `sessionStorage` key (the journey params) beside `yoid.person`.

## Apply

```sh
cd yocv-prototype
git checkout -b connected-journey main
git am /path/to/patches/yocv/*.patch
```

Checked: the three patches apply cleanly to `main` at `7de5915` and give the same tree as the branch.

## Test

```sh
npm test                    # or YOCV_LIVE_YOMA=0 npm test offline
npm start                   # now http://127.0.0.1:4174
```

New tests: `tests/cv-connected-journey.test.cjs` (params, return check, back-link builder, thread, copy) and four
journey cases at the end of `tests/yoid-flows.test.cjs` (arrival, reload keeps the journey, no params = no change,
offline = no journey).

Our run (Node 22.23.5, `YOCV_LIVE_YOMA=0`): **671 tests, 639 pass, 0 fail, 4 skipped, 28 cancelled**. The 28 cancelled
are the provider timeout tests in `tests/anthropic.test.cjs` and `tests/providers.test.cjs`; they cancel the same way
on unpatched `main` in our environment (Node 22, not the repo's Node 20), so they are not caused by this patch.

By hand: open
`http://127.0.0.1:4174/?as=halima&journey=demo&from=skilllens&yoid=yoid-ng-7f3a0123&listing=opp-job-001&goal=job&plan=plan-001&course=opp-course-001&credential=cred-002&return=http%3A%2F%2Flocalhost%3A3000%2Fopportunities%2Fc0a7e000-0000-4000-8000-000000000301`,
go through the four steps, make the QR, send the application, and check the **Back to the listing** link.

## The port change

YoCV now listens on **4174** by default, because PathFinder uses 4173 and the demo runs all four apps on one laptop.
Set `PORT=4173` in `.env` if you need the old port. A `.env` copied from the old `.env.example` still says
`PORT=4173`: change it, or the journey's `YOCV_URL` (`http://localhost:4174`) will not reach YoCV.

## Open points for the owners

- **Credential title.** `server/card/personas.json` calls `cred-002` "Solar PV Basics: practical challenge passed"; the
  contract's item title is "Solar PV Installation Basics: practical challenge passed". The card uses the contract's;
  the wallet list still shows the persona's. Pick one.
- **Button text colour.** The contract fixes the blue `#4cade9` but not the text. White on it reads at 2.6:1, so the
  button text is ink. Other apps may use white.
- The journey UI is off when the server is not reachable (offline mode), as the Apply section already is.
