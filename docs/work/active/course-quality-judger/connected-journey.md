# Connected journey: Ziqi → PathFinder → SkillLens → YoCV → back to the listing

Status: planning

The four BBS prototypes as one journey for Halima, the shared fictional user. Each phase hands her one named item, shows
it on screen, records it on her YoID (mocked), and links on to the next phase. Phase 4 closes the loop on the Phase 1
listing.

Sources:

- the project lead's "Connected User Journey" brief, 2026-11-28;
- the shared contract `shared-mock-data.json` v1.1 (identical copies in the PathFinder and SkillLens repos);
- a read of each repo at:
  - `anaghataneja-stack/pathfinder-prototype` `69d9f57`
  - `wongkycharisse/skilllens` `72699cc`
  - `greenglow7/yocv-prototype` `7de5915`

These SHAs are in the other teams' repositories, not this one.

## The uncomfortable part first

1. **Ziqi is the broken link.** PathFinder, SkillLens and YoCV already use the shared mock data (Halima, the solar job
   `opp-job-001`, the four handoff names). Ziqi uses live Yoma data and has no Halima, no `opp-job-001`, and no "Save"
   that produces a Saved Opportunity. Phase 1 has to join the shared data before any link helps.
2. **Nobody links forward except PathFinder, and nothing is hosted.**
   - PathFinder builds a "Continue to SkillLens" link, but it shows only when `NEXT_PROTOTYPE_URL` is set, and it never
     is.
   - SkillLens ends on the text "YoCV can show this credential", with no link.
   - YoCV's Apply button records `app-001` on the page and links nowhere.
   - Only SkillLens is deployed (Vercel). PathFinder, YoCV and Ziqi run on a laptop.
3. **There is no shared YoID.** Every "YoID write" is in-page or in-memory and private to one app. A shared YoID store
   is out of scope for prototypes; the plan below makes the YoID thread look and read identically everywhere instead.
4. **My Yoma and PathFinder both build a path from the same data.** The split is decided: Ziqi maps *within* Yoma, and
   PathFinder maps *across* platforms, as the bridge between Yoma and Passport to Earning plus WhatsApp/SMS. But in code,
   PathFinder's only real P2E content is the 13 P2E courses already listed on Yoma; other P2E courses and every imaGen
   challenge are samples, and it has no P2E connection of its own. The split only shows on screen if PathFinder's plan
   visibly does what Yoma can't: steps labelled by platform, and a WhatsApp check-in.
5. **Grades don't match.** The contract gives `opp-job-001` "A-" and the scam job "F"; Ziqi grades A–D and holds back
   a scam as "Not verified". Change the contract to Ziqi's real output, not the reverse.
6. **Port clash.** PathFinder and YoCV both default to port 4173.

## Recommendation

Pick the integration level per transition:

| Transition | Handoff item (exact name) | Level | Why |
| --- | --- | --- | --- |
| Ziqi → PathFinder | **Saved Opportunity** (`opp-job-001`) | Deep link | PathFinder already reads `?from=ziqi&listing=&goal=` and `?journey=demo`. |
| PathFinder → SkillLens | **Learning Plan** (`plan-001`) | Deep link to SkillLens's course page | SkillLens's `/demo/p2e` is already a mock Passport to Earning course page. PathFinder already builds the link; it needs the URL and two more params. |
| SkillLens → YoCV | **Skill Credential** (`cred-002`) | Deep link | YoCV has no embed and needs its own server. A link is a day's work. |
| YoCV → Ziqi listing | **Job Application** (`app-001`) | Deep link back to the same Yoma listing, which shows "Application received" | Closes the loop on the page where the journey started: the diagram's blue arrow. |

**Deep links for all four, not embeds.**

- Embeds would hide four different visual styles inside one frame. A shared handoff card makes the move between apps
  feel deliberate instead.
- SkillLens already supports being embedded; keep that as an optional extra.
- Full integration into Yoma (one sign-in, a real YoID write) is the production answer and needs Yoma's YoID write
  API, which does not exist yet. Name it as the next step; don't build it.

## The journey contract (one page every team implements)

### 1. URL parameters, identical in all four apps

| Param | Meaning | Example |
| --- | --- | --- |
| `journey` | `demo` switches the app to the shared mock data | `demo` |
| `from` | the phase that sent the user | `ziqi`, `pathfinder`, `skilllens`, `yocv` |
| `yoid` | Halima's YoID | `yoid-ng-7f3a0123` |
| `listing` | the Saved Opportunity, carried all the way | `opp-job-001` |
| `plan` | the Learning Plan | `plan-001` |
| `course` | the plan step SkillLens tests | `opp-course-001` |
| `credential` | the Skill Credential | `cred-002` |
| `application` | the Job Application | `app-001` |
| `return` | the URL of the Phase 1 listing, carried all the way so Phase 4 can link back | the Ziqi listing URL, encoded |

Every app passes on all the params it received, plus its own. A page that is opened without the params works as it does
today.

### 2. The handoff card, the same design in all four apps

The card is shown at the top of the receiving app and at the end of the sending app.

- **Arriving:** "From Ziqi · Phase 1" above the item name (**Saved Opportunity**), the item's title, and a
  "Recorded on your YoID (mock-up)" line.
- **Leaving:** "Handing on to PathFinder · Phase 2" above the item name and title, with one primary button:
  "Continue to PathFinder".

It uses Yoma's colours and type; the SkillLens repo already ships `yoma-tokens.css` to copy. The item names match the
slides exactly: Saved Opportunity, Learning Plan, Skill Credential, Job Application.

### 3. The YoID thread, the same strip in all four apps

One four-step strip shows Saved Opportunity → Learning Plan → Skill Credential → Job Application:

- steps before this app are **recorded**;
- this app's own step is **in progress**, then **recorded** once the user completes it;
- later steps are **next**.

The state comes from the app's position and the URL params, so no shared database is needed and every app shows the
same thread. Label it "YoID · yoid-ng-7f3a0123 · mock-up".

### 4. Base URLs from configuration, never hard-coded

`ZIQI_URL`, `PATHFINDER_URL`, `SKILLLENS_URL`, `YOCV_URL`. Local defaults:

| App | Local default |
| --- | --- |
| Ziqi | `localhost:3000` |
| PathFinder | `localhost:4173` |
| SkillLens | `localhost:3001` |
| YoCV | `localhost:4174` |

## Who changes what

### Ziqi (us), about 3 to 4 days [guessing]

- **Z1. Journey data.**
  - A `--journey` option on `start.sh` seeds the shared opportunities as Yoma listings: Sunrise Solar and Passport to
    Earning as organisations; `opp-job-001` and courses 001 to 005; the event; the impact action; the scam job. The
    page fixtures behave as the contract says: course 005 returns 404, and the scam asks for a fee.
  - Ziqi then audits them for real, so the grades on screen are Ziqi's own. Update the contract to match.
- **Z2. Halima.** A demo sign-in (`halima@…`, local only) with the Yoma preferences "Get a job", solar, Kaduna.
- **Z3. Save → Saved Opportunity.** On the `opp-job-001` listing, saving (Yoma's existing save action) shows the
  handoff card "Handing on to PathFinder · Phase 2" with "Plan it with PathFinder", linking to
  `PATHFINDER_URL/?journey=demo&from=ziqi&listing=opp-job-001&goal=job&yoid=…&return=<this listing>`.
- **Z4. Close the loop.** The same listing page, opened with `?from=yocv&application=app-001`, shows "Job Application
  received · Phase 4" with the thread fully recorded. This is the journey's last screen.
- **Z5. YoID thread** on the listing page and on the YoID page (the "My path" card already sits there).
- **Z6. My Yoma as "Your route on Yoma".** Phase 1 is discovery within Yoma: My Yoma maps Halima's route through Yoma's
  own listings, and the solar job is the destination she saves. Its last step changes to "Plan it across P2E and
  WhatsApp with PathFinder", which hands over the Saved Opportunity. My Yoma does not try to sequence P2E steps.

### PathFinder (Anagha), about half a day [guessing]

- Accept and forward `yoid` and `return`.
- Set `NEXT_PROTOTYPE_URL` to SkillLens.
- Add the thread strip and a "Back to the listing" link that uses `return`.
- Show each plan step's platform (P2E, Yoma, WhatsApp), and include at least one WhatsApp/SMS check-in in the demo
  plan, so the cross-platform role is visible next to Ziqi's within-Yoma map.
- Optional: RapidPro's "Next in your journey" message carries the SkillLens link.

### SkillLens (Charisse), about 1 day [guessing]

- `/demo/p2e` accepts the contract's params and maps `course=opp-course-001` to `solar-pv`.
- After `cred-002` is recorded, show "Continue to YoCV", linking to
  `YOCV_URL/?as=halima&journey=demo&from=skilllens&credential=cred-002&listing=opp-job-001&yoid=…&return=…`.
- Add the thread strip.

### YoCV (greenglow7 team), about 1 day [guessing]

- Accept the params. When `credential=cred-002`, tag it "Skill Credential, from SkillLens"; the code for this already
  exists.
- After Apply, show the leaving card "Job Application · back to the listing", linking to `return` with
  `?from=yocv&application=app-001` appended.
- Move the default port to 4174.

### Team leads (SCs), organising

- Agree the contract above in one meeting and freeze it.
- Own one shared copy of `shared-mock-data.json`; today PathFinder and SkillLens each keep their own.
- Match slide wording to the card wording, including the diagram's arrow labels.

## How the demo runs

One laptop, offline once installed. A `journey.sh` script, kept outside this repo, starts all four apps on their ports
with the URLs set, then opens Ziqi signed in as Halima.

The click path:

1. Ziqi: sign in, My Yoma shows her route on Yoma, the A grade and the scam held back; open the solar job, Save.
2. "Plan it with PathFinder" → PathFinder: the plan.
3. "Continue to SkillLens" → SkillLens: the challenge, passed on attempt 2, credential recorded.
4. "Continue to YoCV" → YoCV: the CV with the verified credential, the QR, Apply.
5. "Back to the listing" → the Ziqi listing shows "Job Application received", with the thread complete.

A hosted version needs PathFinder and YoCV deployed (each is a Node server) and Ziqi on a reachable host. Not needed for
a live presentation.

## Order of work

1. Agree the contract (SCs, one meeting).
2. In parallel: Z1–Z5 (us); the param and link changes in PathFinder, SkillLens and YoCV (their owners).
3. The `journey.sh` launcher and a click-through test of the five steps.
4. A rehearsal with the slides; fix wording mismatches.

## Open questions

1. **Decided (2026-11-28):** Ziqi maps within Yoma (My Yoma is part of Phase 1); PathFinder maps across platforms and
   bridges Yoma, Passport to Earning and WhatsApp/SMS. Still open for PathFinder's team: a real P2E connection, or
   labelling the P2E-on-Yoma courses as such until one exists.
2. **Grades in the contract.** Accept Ziqi's real grades (A–D, and "Not verified" for the scam) in place of "A-" and
   "F"?
3. **Who hosts what** if GenU wants to click through it themselves after the presentation.
4. **The diagram's labels.** "A verified opportunity and the user's goal" and "Level 2 credential saved to profile"
   versus the item names "Saved Opportunity" and "Skill Credential": the brief asks for the same name on the slide and
   the screen, so pick one wording per handoff.
