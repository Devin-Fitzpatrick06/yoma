# PathFinder: connected journey patch (for Anagha)

Two commits on top of `anaghataneja-stack/pathfinder-prototype` `main` at `69d9f57`. They make PathFinder Phase 2 of
the four-prototype journey (Ziqi → PathFinder → SkillLens → YoCV → back to the listing), following the shared
journey contract v1 (`journey-contract.md`, in the course-quality-judger folder two levels up from here).

| Patch | Commit message |
| --- | --- |
| `0001-feat-connected-journey-contract-v1-SkillLens-link-Yo.patch` | feat: connected journey (contract v1): SkillLens link, YoID thread, back to the listing |
| `0002-docs-connected-journey-setup-demo-link-and-test-resu.patch` | docs: connected journey setup, demo link and test results |

## What changes

Only when the page is opened with `journey=demo`. Without it PathFinder behaves as before; the existing
`?from=ziqi&listing=…&goal=…` path for real listings is untouched.

- **In:** Ziqi's link 1 (`journey`, `from`, `yoid`, `listing`, `goal`, `return`). Every parameter received is kept.
- **Arriving card** (the existing "From Ziqi" card, reworded): "From Ziqi · Phase 1" / **Saved Opportunity** /
  "Solar Installation Assistant (Trainee)" / "✓ Recorded on your YoID (mock-up)".
- **YoID strip** above the controls: `YoID · yoid-ng-7f3a0123 · mock-up`. Saved Opportunity is recorded, Learning Plan
  is in progress until the plan is saved, and Skill Credential and Job Application are next.
- **"Back to the listing"** at the right of the strip, from `return`, in the same tab. Only `http:` and `https:`
  URLs are followed. Anything else is dropped: the link is hidden and the URL is not passed on to SkillLens.
- **Leaving card** (the existing "Handing on" card, reworded), after "Save my plan": "Handing on to SkillLens ·
  Phase 3" / **Learning Plan** / "Plan step: complete a P2E course (Solar PV Installation Basics)" /
  **Continue to SkillLens** / "✓ Recorded on your YoID (mock-up)". The button is contract link 2, in the **same tab**
  (no `target=_blank` in journey mode):
  `NEXT_PROTOTYPE_URL?journey=demo&from=pathfinder&yoid=…&listing=opp-job-001&goal=job&plan=plan-001&course=opp-course-001&return=…`.
  It passes on every parameter received, plus PathFinder's own; `from` becomes `pathfinder`, `return` goes last and
  unchanged. `course_title` is not sent in journey mode, because link 2 does not have it.
- **Across platforms:** each plan step shows its platform (P2E, Yoma, imaGen), and the plan ends with a "Check-in ·
  WhatsApp / SMS" row: "Weekly check-in on WhatsApp or SMS". The SMS (basic phone) plan text gets the same labels.
  The check-in row is a note on the card, not a plan step, so the plan engine and the RapidPro webhooks are unchanged.
- **Code:**
  - `engine/journey.js` (new): link 2, the `return` check and the thread states. A classic script with no DOM, like
    `engine/plan-engine.js`. Served by `lib/server.mjs` and cached by `sw.js` (shell cache now `pf-shell-v7`).
  - `pathfinder.html`: the cards, strip, back link, platform labels and check-in row. Colours come from the Yoma
    tokens only (the html test still passes). The button is Yoma blue with purple text, because white on that blue is
    only 2.5:1 and purple is about 5.5:1, the same choice as the SkillLens patch.
  - `serve.mjs`: if `NEXT_PROTOTYPE_URL` is empty, `SKILLLENS_URL` + `/demo/p2e` is used.
  - 8 new unit tests in `test/journey.test.mjs`, one more check in `test/server.test.mjs`; the e2e journey scenario
    now checks the exact wording and link, and a new scenario checks a bad `return` and the page without the journey.
- **Docs:** COHESION.md section 6, DEMO.md (the journey link), TESTING.md and README (new numbers, the setting),
  `.env.example`, and a short update at the end of HANDOFF.md.

## Decisions you may want to change

- **"Recorded" respects the Skills Backpack.** The Learning Plan shows as recorded (strip and tick line) only once the
  plan is saved **and** the Backpack is on. If Halima taps "Not now", it stays "in progress" and the leaving card has
  no tick line, because PathFinder writes nothing to YoID before a yes.
- **The pilot access code (`code`) is never passed on** to SkillLens, although the contract says to pass on every
  parameter.
- **The journey wording is English only**, exactly as in the contract, so it is not in the translation tables (the
  test that every English message has Hausa and Yoruba versions does not apply). The rest of the chat still follows
  the chosen language.
- **`.env.example` now sets `NEXT_PROTOTYPE_URL=http://localhost:3001/demo/p2e`** for the local demo. Leave it empty
  for the pilot, or the button appears for real users too.

## How to apply

```bash
cd pathfinder-prototype
git checkout -b connected-journey main
git am /path/to/patches/pathfinder/*.patch
```

## How to test

```bash
npm test                    # 106 unit and API tests (was 98)
npm run e2e                 # 24 browser scenarios (was 23); needs Playwright and Chromium
PORT=4173 NEXT_PROTOTYPE_URL=http://localhost:3001/demo/p2e node serve.mjs
```

Open
`http://localhost:4173/?journey=demo&from=ziqi&yoid=yoid-ng-7f3a0123&listing=opp-job-001&goal=job&return=http%3A%2F%2Flocalhost%3A3000%2Fopportunities%2Fc0a7e000-0000-4000-8000-000000000301`,
then tap English → 20 → Yes, use my profile → Skip → Another state → 6–10 hours → Very little → Quick → Save my plan →
Yes, activate it. "Continue to SkillLens" then links to:

```
http://localhost:3001/demo/p2e?journey=demo&from=pathfinder&yoid=yoid-ng-7f3a0123&listing=opp-job-001&goal=job&plan=plan-001&course=opp-course-001&return=http%3A%2F%2Flocalhost%3A3000%2Fopportunities%2Fc0a7e000-0000-4000-8000-000000000301
```

Also open `http://localhost:4173/` with no parameters. It should look as it does on `main`.

## Not done

- The optional RapidPro "Next in your journey" message carrying the SkillLens link.
- Hausa, Pidgin and Yoruba versions of the journey wording (needs the native-speaker review anyway).
