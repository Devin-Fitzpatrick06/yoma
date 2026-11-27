# Ziqi iteration 4: demo walkthrough

Status: in-progress — written before the integrated demo run. URLs and accounts are checked against `start.sh`. The
screens are the Web team's and were not seen when this was written: check each step once on the running demo and fix
the wording here.

A click-by-click script for the five steps in `iteration-4.md` ("Demo walkthrough this must support"). It takes about
15 minutes. It is for Yoma staff only, like the rest of the demo (`scripts/course-quality-demo/README.md`).

## Before you present

1. Start the demo with `scripts/course-quality-demo/start.sh` and wait for the summary. Check that it prints:
   - **By type**: courses, impact actions and jobs, each more than 0;
   - **Synthetic pathway completions (demo data, labelled)**: more than 0;
   - the Ziqi iteration 4 links: My Yoma, YoID, partner new listing, journey map preview, Ziqi draft and admin previews.
2. Use one browser window per role, or a private window for each, so sign-ins do not overlap.
3. Have the caveats ready (end of this page). Say them as you go; do not save them for questions.

Sign-ins (public local defaults from `src/api/docker-compose.yml`, not real accounts). Password for all: `P@ssword12`.

| Role | Email |
| --- | --- |
| Youth | `testuser@gmail.com` |
| Partner (admin of Demo Learning Partner), also the reviewer and admin | `testadminuser@gmail.com` |
| Partner only | `testorgadminuser@gmail.com` |

Demo Learning Partner's id is `c0a7e000-0000-4000-8000-000000000001` (`<ORG_ID>` below).

If a live screen is not ready, each step has a preview page that runs on fixtures and needs no sign-in:
`/my-yoma-preview?view=map` (the journey map), `/ziqi-draft-preview` (the partner's pre-publish panel) and
`/ziqi-admin-preview` (the admin page). Say plainly that it is a preview.

## 1. Youth: first visit, two questions, the journey map (5 minutes)

1. Open `http://localhost:3000` and choose **Sign in**. Sign in as `testuser@gmail.com`.
2. With the switch on (only in this demo), a youth without a path lands on **My Yoma** (`/my-yoma`), not the home page.
   - Say: "Ziqi starts from what Yoma already knows: the YoID card flies in with what this youth has done."
3. Answer the questions on the slides. There are at most two: Yoma's own preferences already answer the rest.
   - For the goal, choose **Digital marketer** (or any goal: every demo goal has a path with an impact step).
   - Say: "We only ask what Yoma's preferences do not already tell us."
4. Watch "Building your path": the line draws itself and the step cards fly in and lock onto it.
5. Scroll the map from **Start** (your YoID) through the stages (learn, practise, prove) to **First job**, where the
   matched jobs are. Point out on a step card:
   - the type icon: course, or **impact action** (a practise step from live Yoma's Task listings);
   - the Ziqi grade and the partner badge;
   - hours and the skill it adds;
   - **Usually go on to**: "young people who completed this usually go on to …", with the share and n. Say: "These
     figures come from synthetic demo learners, as the note on the card says. On live Yoma they appear once 20 youth
     have completed a step."
   - the drop-off slot: **"Drop-off: collecting from the pilot"**. Say: "We show no drop-off number until the pilot
     has measured one."
6. **Lock** a step (it stays when the path is rebuilt). Then **Swap** another step: up to three alternatives slide in.
   Choose one.

## 2. Youth: YoID shows "My path" (1 minute)

1. Still signed in as the youth, open `http://localhost:3000/yoid`.
2. Point out the **My path** card: the goal, how many steps are done, and completed steps shown in place, each with the
   existing verified completion and its credential.
   - Say: "Nothing new is issued yet. A milestone credential per stage is proposed, and it waits for Yoma's v5
     credential schemas (`data-mapping.md`, section 6)."
   - The demo youth has no completions of their own. If the card shows 0 done, say so: the demo learners with
     completions cannot sign in.

## 3. Partner: Ziqi checks a course before it is published (4 minutes)

1. In a second window, sign in as `testadminuser@gmail.com`.
2. Open `http://localhost:3000/organisations/c0a7e000-0000-4000-8000-000000000001/opportunities/create`.
3. Fill in a course, leaving two things for Ziqi to catch:
   - Type: **Learning**. Title: for example "Social Media Basics (demo draft)". Fill in the description and summary.
   - Course link: `http://demo-fixtures.localtest.me/courses/community-health-gone`, a demo page that returns
     "page not found" on purpose.
   - Leave the **time commitment** empty.
4. The **Ziqi** panel predicts **"Not verified yet"** and lists the fixes, in the partner's words, for example: the link
   does not open, the time commitment is missing, the dates look like placeholders, and the format (course or
   webinar) is unclear. Expect three or four; the number depends on what you filled in. A fix for a field on the
   step on screen has a **Go to** button; a fix on another step says which step it is on (no jump, so nothing the
   partner typed is lost). Choose the **type first**: until then the panel asks for it.
   - Say: "This is the same rule engine that checks published listings, run on the draft. Nothing is stored."
   - If the panel lists other fixes, follow what it says; the point is that each fix names a field.
5. Fix both: set the link to `http://demo-fixtures.localtest.me/courses/digital-marketing-cohort` (a working demo page)
   and set a time commitment, for example 6 hours.
6. The panel now predicts **Verified**. Choose **Publish** (or Submit, whichever the form offers).
   - Say: "The prediction is not a promise: the published listing is checked again, as every listing is."
   - If an AI key is configured, "Draft this for me" offers suggestions. In the demo it is normally off; say so if asked.

## 4. Partner badges, up to Platinum (2 minutes)

1. Open the partner page, for example
   `http://localhost:3000/organisations/c0a7e000-0000-4000-8000-000000000001/course-quality` (the partner's Ziqi page,
   with the badge card), and the learner course list at `http://localhost:3000/opportunities`.
2. Point out the partner badge on the partner page and on the course cards: Bronze, Silver, Gold or Platinum.
   - Say: "A badge is computed from Ziqi's grades every time, never stored. A partner with fewer than 10 graded courses
     shows 'not enough graded yet' rather than no badge."
   - Grades are percentiles of the whole catalogue, and one provider is most of it. Say so if you show Alison
     (`badges.md`, "Caveat to show with any badge").
3. If no organisation in the demo reaches Platinum, show the level on `/ziqi-admin-preview` or `/my-yoma-preview` and
   say it is a preview of the level.

## 5. Admin: pathway health and drop-off (2 minutes)

1. Still signed in as `testadminuser@gmail.com`, open `http://localhost:3000/admin/course-quality`.
2. Open the **pathway health** panel. Point out the number of steps tracked and events recorded since the demo started:
   the step events from step 1 (shown, started, swapped) are real events from this session.
3. Point out drop-off: **"Collecting from the pilot"** on the panel and on a reviewer card, never a number.
   - Say: "Drop-off needs real youth and at least 20 of them per step. We reserved the place for it and we collect from
     now on; we do not invent it."

## The honest caveats (say them)

- **Demo data is labelled.** The live listings (2,419 courses, 41 impact actions, the live jobs) are real public Yoma
  data. Everything about participation is not: the views, starts, completions, feedback and the "usually go on to"
  chains come from 50 synthetic demo learners, and My Yoma says so on every figure (`Pathway__DemoDataNote`). The
  chains (course → impact action → course) are picked by matching each goal's stages, skills and categories. They show
  how the feature reads, not how youth behave.
- **Drop-off is "collecting from the pilot".** No drop-off figure exists yet and none is shown.
- **The switches are off in production.** My Yoma (`Pathway:Enabled`, `NEXT_PUBLIC_PATHWAY_ENABLED`) and the
  pre-publish check (`CourseQuality:PrePublish:Enabled`, `NEXT_PUBLIC_PREPUBLISH_ENABLED`) are on only in this demo's
  `demo.env`. Impact action checks (`CourseQuality:VerifyTypes` with ImpactAction) are demo only too.
- **Impact actions are checked, not graded.** Ziqi checks their listings; a grade waits until "good" is defined for
  them.
- **Badges and grades are provisional.** The thresholds are placeholders until the project lead sets them.
- **Credentials:** nothing new is issued. The milestone credential waits for Yoma's schemas v5.
