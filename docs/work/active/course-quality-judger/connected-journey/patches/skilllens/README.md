# SkillLens: connected journey patch (for Charisse)

Two commits on top of `wongkycharisse/skilllens` `main` at `72699cc`. They make `/demo/p2e` Phase 3 of the
four-prototype journey (Ziqi → PathFinder → SkillLens → YoCV → back to the listing), following the shared
journey contract v1 (`journey-contract.md`, in the course-quality-judger folder two levels up from here).

| Patch | Commit message |
| --- | --- |
| `0001-feat-connected-journey-on-demo-p2e-journey-contract-.patch` | feat: connected journey on /demo/p2e (journey contract v1, Phase 3) |
| `0002-docs-connected-journey-setup-NEXT_PUBLIC_YOCV_URL-an.patch` | docs: connected journey setup, NEXT_PUBLIC_YOCV_URL and progress log |

## What changes

Only when `/demo/p2e` is opened with `journey=demo`. Without it the page renders as before.

- **In:** reads PathFinder's link (`journey`, `from`, `yoid`, `listing`, `goal`, `plan`, `course`, `return`).
  `course=opp-course-001` opens Solar PV Installation Basics. `yoid=yoid-ng-7f3a0123` is Halima, passed to the embed
  as `learner=yp-001`.
- **Arriving card** at the top: "From PathFinder · Phase 2" / **Learning Plan** / "Plan step: complete a P2E course
  (Solar PV Installation Basics)" / "✓ Recorded on your YoID (mock-up)".
- **YoID strip** `YoID · yoid-ng-7f3a0123 · mock-up`. It shows Saved Opportunity and Learning Plan as recorded,
  Skill Credential as in progress, and Job Application as next. In journey mode it takes the place of the detailed
  thread card, so the screen shows one thread.
- **Leaving card** once the embed posts `skilllens:yoid` with `cred-002` recorded: "Handing on to YoCV · Phase 4" /
  **Skill Credential** / title / **Continue to YoCV** / "✓ Recorded on your YoID (mock-up)". The button opens
  contract link 3 in the same tab:
  `NEXT_PUBLIC_YOCV_URL/?as=halima&journey=demo&from=skilllens&yoid=…&listing=…&goal=…&plan=…&course=…&credential=cred-002&return=…`.
  The link passes on every parameter the page received. Parameters outside the contract go after the contract's own.
- **"Back to the listing"** in the header, from `return`. Only `http:` and `https:` URLs are followed. Anything else
  is dropped: the link is hidden and the URL is not passed on to YoCV.
- **Code:**
  - `lib/journey.ts`: parameters, mapping, link 3, thread states.
  - `components/Journey.tsx`: the card and the strip, built only from Yoma tokens. A new `--color-journey` theme
    token holds the Yoma blue. The button text is brand purple, because white on that blue is only 2.5:1 and purple
    is 5.5:1.
  - `app/demo/p2e/page.tsx` is now a small server wrapper that parses the parameters. The client page moved to
    `app/demo/p2e/HostDemo.tsx`, which the patch shows as a copy.
  - 13 new tests in `lib/journey.test.ts`.
- **Docs:** a new "Connected journey" part in the README, INTEGRATION.md section 9, a row in HANDOVER.md, the new
  variable in `.env.example`, and a new row in the PROGRESS.md log.
- **Not changed:**
  - The embed (`/embed/skilllens`), its `postMessage` events and the i18n text "Next: YoCV can show this
    credential…". The button lives on the host page. A real P2E host that is not part of the journey has no
    button, so the embed's text stays as it is.

## New setting

`NEXT_PUBLIC_YOCV_URL`: YoCV's address (`YOCV_URL` in the contract). The default is `http://localhost:4174`.
It is read at build time, like `NEXT_PUBLIC_P2E_ORIGIN`, so on Vercel you set it before the build.

## How to apply

```bash
cd skilllens
git checkout -b connected-journey main
git am /path/to/patches/skilllens/*.patch
```

## How to test

```bash
npm ci
npm run check              # lint, types, 112 tests, offline eval, build
npm run setup              # if you have no .env.local yet (sets PASS_SIGNING_SECRET; without it the dev server
                           # rejects the credential write with 409, because each route has its own random key)
NEXT_PUBLIC_YOCV_URL=http://localhost:4174 npm run dev -- -p 3001
```

Open
`http://localhost:3001/demo/p2e?journey=demo&from=pathfinder&yoid=yoid-ng-7f3a0123&listing=opp-job-001&goal=job&plan=plan-001&course=opp-course-001&return=http%3A%2F%2Flocalhost%3A3000%2Fopportunities%2Fc0a7e000-0000-4000-8000-000000000301`,
then follow these steps:

1. Tap Challenge yourself and choose English.
2. For Level 1, give the attempt-1 answer from scenario R6.
3. Tap Try Level 2 and give the attempt-2 answer from R6.
4. Tap Save, then tap Add Skill Credential to my YoID.

"Continue to YoCV" then appears, and in simulated mode it links to:

```
http://localhost:4174/?as=halima&journey=demo&from=skilllens&yoid=yoid-ng-7f3a0123&listing=opp-job-001&goal=job&plan=plan-001&course=opp-course-001&credential=cred-002&return=http%3A%2F%2Flocalhost%3A3000%2Fopportunities%2Fc0a7e000-0000-4000-8000-000000000301
```

Also check `http://localhost:3001/demo/p2e` with no parameters. It should look as it does on `main`.

## Worth knowing

- `/demo/p2e` is now rendered per request (`ƒ` in the build output) instead of statically, because it reads the URL
  on the server. That avoids a flash of the non-journey page. The page looks the same.
- Checked with Node 22.23.5. The repo asks for Node 24, which was not available here; `npm ci` warns, and everything
  passes.
- `docs/PROGRESS.md`'s testing table still says "76 tests" from an earlier sprint. This patch does not touch that line.
