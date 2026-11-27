# Ziqi iteration 4: answer Yoma's AI-use-case note, and a visual journey map

Status: planning

Source: "Yoma AI use cases mapped against the BBS concepts" (Yoma, 19 Nov 2026, working draft). Builds on
`handoffs/2026-11-27-a.md`. Branch: `feature/course-quality-judger`. Everything below ships **into the demo first**,
behind production switches that are off by default (see W0).

## Ground truths from the project lead (2026-11-27)

- **Ziqi owns pathways.** PathFinder's goal-to-plan work is treated as input, not a parallel product.
- **Public partner badges are fine with partners.**
- **No drop-off data exists yet.** Reserve where it will live and where it will show, collect from the pilot, and
  show nothing invented in the meantime.
- The product name is **Ziqi** (Yoma's spelling), not Zeki.
- How Ziqi combines with the other BBS platforms (PathFinder, YoCV, SkillLens, QR) comes later: keep interfaces clean,
  build no integrations yet.

## What the note asks, and the workstream that answers it

| Note's ask | Workstream |
| --- | --- |
| Use case 28/30: co-author and check listings before they publish (partner side, highest value) | **W1** pre-publish check |
| Ziqi: "add provider badges (bronze to platinum) so quality is rewarded" | **W2** badges |
| Use case 25/30: "reuse the preferences young people now set on yoma.world" | **W3** pathways read Yoma preferences |
| PathFinder: "write the plan and each completed step back to YoID" | **W3** |
| Use case 21/30: "pathways across course, impact task and job", "young people who completed A usually go on to B" | **W4** mixed-step pathways |
| Ziqi: "extend to events, impact actions and jobs" | **W4** (impact actions); jobs and events are done |
| Use case 23/30: drop-off as a nudge trigger and a quality signal back to Ziqi | **W5** reserved, no data yet |
| Data structuring: "use the new opportunity fields and the agreed credential schemas (v5)" | **W6** |
| Lead: a fly-in first visit and a scrollable start-of-career-to-job map | **W7** journey map |

## W0. Switches and naming (first, small)

- Production switches, off by default: API `Pathway:Enabled`, `CourseQuality:PrePublish:Enabled`; web
  `NEXT_PUBLIC_PATHWAY_ENABLED`, `NEXT_PUBLIC_PREPUBLISH_ENABLED`. `demo.env` and the demo web build turn them on.
  Off means: no nav tab, `/my-yoma` 404, endpoints 404, partner form unchanged. Visible changes need Jason's approval
  before they ship anywhere but the demo.
- Rename every learner-, partner- and reviewer-facing "Course Quality" / "Zeki" label to **Ziqi**. Code names stay.
- Demo housekeeping: `start.sh` prints the My Yoma link and counts courses and jobs separately.

## W1. Pre-publish check in the partner portal (highest value)

- **API:** `POST /api/v3/courseQuality/draft/check` (OrganisationAdmin of that organisation, Admin). Takes the draft
  listing as the create/update request already sends it; runs the same rules as after publishing for its type
  (Learning, Event, Job, Impact Action) without storing anything; returns the predicted status, score, each check, and
  the fix note in partner wording. Link check uses the existing safe checker with a short timeout; an unreachable link
  is "could not confirm", never a hard fail, while the partner is typing.
- **Web:** a "Ziqi check" panel on the create and edit listing form (`organisations/[id]/opportunities/[opportunityId]`):
  live as fields change (debounced), a predicted badge (Verified, Needs a look, Not verified yet), and each fix with a
  "jump to field" link. Never blocks saving; it advises.
- **"Draft this for me" (phase 2 of W1):** suggested summary, dates and skills from the course URL through the
  existing Anthropic provider. Off in the demo unless an API key is configured; the panel shows the suggestion
  slot either way.
- **Done when:** a partner sees the same verdict before publishing as Ziqi gives after; tests prove the draft check
  and the post-publish check agree on the same listing.

## W2. Partner badges: bronze to platinum, public

- Add **Platinum** above Gold (config `Pathway:PartnerBadges:Levels`); placeholder thresholds, e.g. 75% A or B and at
  most 2% held back, with at least 25 graded courses.
- Public: the badge shows on the organisation's public page, on each course card and learner card ("Gold partner"),
  and in My Yoma next to the provider. A badge carries its review date (start of next quarter).
- Badge rules page for partners: what each level needs, and their own next step (the existing `nextLevelHint`).
- **Done when:** every graded partner in the demo shows a level or "Not enough graded courses", consistently on all
  surfaces.

## W3. Pathways start from Yoma's own preferences, and write back to YoID

- **Read first:** `UserPreferences` (goal, time commitment, languages, categories, self-attested skills,
  engagement types) through the existing `GET user/preferences`. Map Yoma's goals (Get a job, Learn new skills, Start a
  business, Volunteer / make an impact, Attend events) and categories to our career goals.
- **Ask only what is missing:** usually the career (which job) and the starting point. Answers that have a home in
  Yoma's preferences are saved there (time, languages, skills, categories), so the next product reads them too; only
  the career and starting point stay in the pathway profile.
- **Write back to YoID:** the plan appears on the YoID page (`/yoid`) as a "My path" card; each completed step is the
  existing verified completion (and its credential) shown in place on the path. A "Pathway milestone" credential per
  completed stage is proposed with W6's schemas, not built before them.
- **Done when:** a user who filled in Yoma's preferences answers at most two questions; their path shows on `/yoid`.

## W4. Pathways across course, impact task and job; impact actions in Ziqi

- **Steps of three kinds:** each goal's stages can hold Learning, Impact Action (and Task) steps, ending in jobs: for
  example learn → practise in an impact action → apply. Configured in `Pathway:Goals` per stage (`types`).
- **Ziqi on impact actions:** listing checks via `CourseQuality:VerifyTypes` (Impact Action added); no grade until
  "good" is defined for them.
- **"Usually go on to B":** from completion records, per step, "young people who completed this usually go on to …"
  with the share and n, hidden below 20 youth. The demo's synthetic learners make it visible and are labelled.
- **Demo data:** import live Task / Impact Action listings like courses and jobs.
- **Done when:** each demo goal's path contains at least one impact step, and each step can show a "usually go on to".

## W5. Drop-off: reserved, collecting, nothing invented

- **Where it lives:** a `PathwayStepEvent` table (user, path version, step, opportunity, event: Shown, Started,
  Completed, Swapped, Dismissed; timestamp), written from now on by My Yoma and by completions. Aggregates only, minimum
  group size 20.
- **Where it shows:** a reserved slot per step ("Drop-off: collecting from the pilot") in My Yoma's reviewer view and
  in Ziqi's reviewer card; a "pathway health" panel on the admin page with the same empty state.
- **Nudge hook:** an interface for a re-engagement trigger (step started, no progress in N days), with a no-op
  implementation; RapidPro or WhatsApp comes with the platform-combining work.
- **Feedback to Ziqi:** a `DropOff` grade factor at weight 0, neutral without data, like Outcomes.
- **Done when:** events are recorded in the demo and every drop-off surface shows its empty state, never a number.

## W6. Data model: new opportunity fields and credential schemas v5

- Read job and course facts from the custom-field framework first (as the job checks already do), the text second.
- Ask Yoma for credential schemas v5; until they arrive, keep the milestone credential (W3) as a documented proposal
  mapped to the current Opportunity schema attributes.
- **Done when:** a mapping table (Ziqi field → custom field or schema attribute) is in this folder and used by W1 and W3.

## W7. The journey map: fly-in first visit, scroll from start of career to a job

- **First visit after signing in:** with the switch on, a signed-in youth without a path lands on My Yoma instead of
  the home page. The YoID card flies in, then the (at most two) questions as full-screen slides with chips that pop.
- **"Building your path":** a short animation, then the map assembles: the path line draws itself and step cards fly
  in one by one and **lock** onto it.
- **The map:** one continuous scrollable journey (vertical on phones, horizontal-with-scroll on wide screens):
  *Start* (your YoID: what you have done) → stages (learn, practise, prove) → *First job* (the matched jobs as the
  destination). A "You are here" marker sits at the first unfinished step; completed steps are filled in.
- **Each step card:** type icon (course, impact action, job), Ziqi grade and partner badge, hours, the skill it adds,
  "usually go on to", and the reserved drop-off slot (W5). **Lock** pins a step; **Swap** opens up to three
  alternatives that slide in. Locked steps survive a rebuild.
- **Celebration:** finishing a step or a stage plays a short confetti burst and moves the marker.
- **Craft rules:** CSS keyframes, transitions and View Transitions only (no new dependencies); Yoma's colours and
  fonts; respects `prefers-reduced-motion` (everything appears in place, no motion); works at 320 px; keyboard and
  screen-reader friendly. Design spec approved by Jason before it leaves the demo.
- **Done when:** the first visit is a single continuous flow from sign-in to a full map in under a minute, and the
  map scrolls from Start to First job without a page change.

## Order of work

| Phase | Work | Why first |
| --- | --- | --- |
| 1 | W0 switches and naming; W2 Platinum and public badges | Small, unblock everything, reduce release risk |
| 2 | W1 pre-publish check (API, then the portal panel); W3 read preferences and ask only what is missing | Highest value for Yoma (partner side); stops asking twice |
| 3 | W4 mixed steps and impact actions; W5 step events and reserved slots | Needed by the map's cards |
| 4 | W7 journey map, then the YoID "My path" card (W3 write-back) | Built on top of 2 and 3 |
| 5 | W6 mapping table; "draft this for me"; demo walkthrough and handoff | Depends on Yoma's schemas and an AI key |

Teams, if run in parallel as before: **API** (W1 API, W2, W3 read/write, W4, W5), **Web** (W1 panel, W7, YoID card,
badges on surfaces), **Data and demo** (W4 imports, W6 mapping, switches in the demo, walkthrough). One migration owner
(the API team): `PathwayStepEvent` and any badge changes go in one migration.

## Demo walkthrough this must support

1. Sign in as a youth → fly-in → two questions → the map assembles → scroll Start → First job → lock and swap a step.
2. Open YoID → "My path" card with completed steps.
3. Sign in as a partner → create a course → the Ziqi panel predicts "Not verified yet" with two fixes → fix them → it
   predicts Verified → publish.
4. Partner page and course cards show the partner's badge, up to Platinum.
5. Admin → pathway health and drop-off slots show "collecting from the pilot".

## Open for later (the lead's next instructions)

- Combining PathFinder (RapidPro check-ins), YoCV (YoID public profile), SkillLens (credentials into YoID) and the QR
  employer confirmation with Ziqi.
