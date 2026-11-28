# Journey contract v1: the links, card and thread every prototype implements

Status: in-progress

The exact rules behind [connected-journey.md](connected-journey.md). All four prototypes implement them the same way.
The data is the shared mock data v1.1 (`shared-mock-data.json`), the same file PathFinder and SkillLens already carry.
Everything is fictional and every screen that uses it says "mock-up" or "sample data".

## Phases and handoff items

| Phase | App | Key | Hands on | Item id | Item title |
| --- | --- | --- | --- | --- | --- |
| 1 | Ziqi (Yoma) | `ziqi` | **Saved Opportunity** | `opp-job-001` | Solar Installation Assistant (Trainee) |
| 2 | PathFinder | `pathfinder` | **Learning Plan** | `plan-001` | Plan step: complete a P2E course (Solar PV Installation Basics) |
| 3 | SkillLens | `skilllens` | **Skill Credential** | `cred-002` | Solar PV Installation Basics: practical challenge passed |
| 4 | YoCV | `yocv` | **Job Application** | `app-001` | Application to Solar Installation Assistant (Trainee) |

## Addresses (local demo; each set by configuration, never hard-coded)

| App | Setting | Local default | Journey entry |
| --- | --- | --- | --- |
| Ziqi | `ZIQI_URL` | `http://localhost:3000` | `/opportunities/c0a7e000-0000-4000-8000-000000000301` (the Yoma listing for `opp-job-001`) |
| PathFinder | `PATHFINDER_URL` | `http://localhost:4173` | `/` (serves `pathfinder.html`) |
| SkillLens | `SKILLLENS_URL` | `http://localhost:3001` | `/demo/p2e` |
| YoCV | `YOCV_URL` | `http://localhost:4174` | `/` |

YoCV's default port moves from 4173 to 4174, because PathFinder uses 4173.

## URL parameters

| Param | Value in the demo | Set by |
| --- | --- | --- |
| `journey` | `demo` | every link |
| `from` | the sending app's key | every link |
| `yoid` | `yoid-ng-7f3a0123` | every link |
| `listing` | `opp-job-001` | every link |
| `goal` | `job` | Ziqi |
| `plan` | `plan-001` | PathFinder onward |
| `course` | `opp-course-001` | PathFinder onward |
| `credential` | `cred-002` | SkillLens onward |
| `application` | `app-001` | YoCV (on the link back) |
| `return` | the absolute URL of the Ziqi listing, URL-encoded | Ziqi; every app passes it on unchanged |

Rules:

- Pass on every parameter you received, plus your own.
- A page opened without `journey=demo` behaves exactly as before.
- Only `http:` and `https:` return URLs are followed. Anything else is ignored, and the "back to the listing" link is
  hidden.

## The links

1. **Ziqi → PathFinder:**
   `PATHFINDER_URL/?journey=demo&from=ziqi&yoid=…&listing=opp-job-001&goal=job&return=…`
2. **PathFinder → SkillLens:**
   `SKILLLENS_URL/demo/p2e?journey=demo&from=pathfinder&yoid=…&listing=opp-job-001&goal=job&plan=plan-001&course=opp-course-001&return=…`
3. **SkillLens → YoCV:**
   `YOCV_URL/?as=halima&journey=demo&from=skilllens&yoid=…&listing=opp-job-001&goal=job&plan=plan-001&course=opp-course-001&credential=cred-002&return=…`
4. **YoCV → Ziqi**, back to the listing: `<return>?journey=demo&from=yocv&yoid=…&application=app-001`
   (use `&` if `return` already has a query).

Every link opens in the **same tab**: the journey is one path, not four tabs.

## The handoff card

One card per app; the wording below is exact, in sentence case as written (no uppercase transform on the eyebrow).
Yoma colours: white card, `#41204b` purple heading, `#387f6a` green "recorded" text, a blue (`#4cade9`) button with
purple (`#41204b`) text, Nunito headings, Open Sans body, 12 px radius. The button is the same in all four apps:
white text on that blue is about 2.5:1 and fails contrast.

- **Arriving** (top of the receiving app, when `journey=demo`):
  - eyebrow: `From {Previous app} · Phase {n-1}`
  - title: `{Item name}` (bold), then the item title
  - line: `✓ Recorded on your YoID (mock-up)`
- **Leaving** (when this app's step is done):
  - eyebrow: `Handing on to {Next app} · Phase {n+1}`
  - title: `{Item name}`, then the item title
  - primary button: `Continue to {Next app}`
  - line: `✓ Recorded on your YoID (mock-up)`
- **Phase 4 → back to Phase 1:** eyebrow `Back to Ziqi · Phase 1`, title `Job Application`, button
  `Back to the listing`.
- **Ziqi, the journey's last screen**, the listing opened with `from=yocv`:
  - eyebrow: `From YoCV · Phase 4`
  - title: `Job Application received`, then the item title
  - line: `✓ Journey complete: all four items are on your YoID (mock-up)`

## The YoID thread

The same four-step strip in every app:

- label: `YoID · yoid-ng-7f3a0123 · mock-up`
- steps: `Saved Opportunity`, `Learning Plan`, `Skill Credential`, `Job Application`

Each step's state, for the app at phase `n`:

| Steps | State |
| --- | --- |
| Before `n` | `recorded` (green tick) |
| Step `n` | `in progress` until the app's own action is done, then `recorded` |
| After `n` | `next` (grey) |
| All four, on Ziqi's last screen | `recorded` |

The state comes from the phase and the URL; there is no shared store. That is honest for a prototype: Yoma has no
YoID write for these yet.

## Ziqi's data mapping (shared ids → Yoma)

| Shared id | Yoma opportunity id | Type |
| --- | --- | --- |
| `opp-job-001` | `c0a7e000-0000-4000-8000-000000000301` | Job |
| `opp-job-002` | `…000000000302` | Job (scam: asks for a fee) |
| `opp-course-001` to `005` | `…000000000311` to `…315` | Learning (`005` has a dead link) |
| `opp-event-001` | `…000000000321` | Event |
| `opp-impact-001` | `…000000000331` | Impact action |

Organisations:

| Shared id | Yoma organisation id |
| --- | --- |
| `org-sunrise` | `c0a7e000-0000-4000-8000-000000000011` |
| `org-p2e` | `…000000000012` |
| `org-greenyouth` | `…000000000013` |
| `org-arewahub` | `…000000000014` |
| `org-fasttrack` | `…000000000015` |

The demo sign-in for Halima is `halima.yusuf@example.org`, password `P@ssword12` (local Keycloak only, fictional).

## Ziqi's actual grades

Ziqi grades courses and jobs A to D and does not grade a listing it holds back, so the shared file's `A-` and `F` (and
`D` for the dead-link course) are not what Ziqi shows. Measured on 2026-11-28 on the sandbox demo database (2,015
graded courses), with Ziqi's rule-based checks, as `start.sh --journey` runs them (`seed/assess-journey.mjs`):

| Shared id | Shared file says | Ziqi shows |
| --- | --- | --- |
| `opp-job-001` | A- | Verified, job grade **A** (score 92) |
| `opp-job-002` | F, "asks for a fee" | **Held back** (Needs review), no grade: "The listing asks applicants to pay a fee or deposit" |
| `opp-course-001` | A, gold partner | Verified, **A** "Top pick" |
| `opp-course-002`, `003` | none | Verified, C |
| `opp-course-004` | none | Verified, B |
| `opp-course-005` | D, dead link | **Not verified**, no grade: "The course link returned page not found (404)" |
| `opp-event-001` | B | Verified (events get the listing checks only, no grade) |
| `opp-impact-001` | none | Verified (listing checks only, no grade) |

- Course grades are relative to the catalogue, so a demo database with fewer graded courses can move 002 to 004 by a
  band. The A for `opp-course-001` and `opp-job-001` held on this database.
- With Ziqi's AI on (`qwen2.5:3b` on a CPU), the model sent courses 001 to 003 to review ("the dates on the course page
  differ"), which they do not: the pages show no dates. `start.sh` runs these checks without the AI, so the demo shows
  the results above.
- The other prototypes should show these, not the shared file's grades (connected-journey.md, open question 2).
