# Course Quality demo

Runs the Course Quality prototype on this Mac. Everything uses placeholder settings, and the
assessment is switched on through the demo's environment, so the product's own configuration does
not change.

**This demo is for Yoma staff only. Do not show it to learners or partners as a live service.** The
badges and costs are provisional: the scoring criteria are placeholders until the project lead sets
the final ones, and no AI ran. "Verified by Yoma" here means the current rules passed, and a cost is
only what the page reader found. Treat every result as a prototype output.

## Before you start

- Open Docker Desktop and wait until it says it is running.
- Quit anything using ports 80, 3000, 5050 or 8080. The script tells you if one is busy.
- The first start needs internet.

You do not need .NET or Node on your Mac, because everything runs in Docker.

## Run it

From the repo folder:

```bash
scripts/course-quality-demo/start.sh
```

When it finishes, it prints the three links, the test sign-ins, and a summary. Open the course list only after that:
the website starts last, once every course is in, and `start.sh` loads the course list once so the page it keeps for
5 minutes already shows the courses.

What a start does depends on whether a saved demo database is in `scripts/course-quality-demo/.cache/`:

| Start | What happens | Time on this Mac |
| ----- | ------------ | ---------------- |
| Saved database present | Loads it, sets the named courses 101 to 108 to today's dates, assesses those 8 again and regrades | about 2.5 minutes |
| No saved database (the default) | Imports every live Yoma course and 2,000 live jobs, assesses and grades a sample of 40 courses, opens the website. Saves nothing | about 8 minutes |
| `--full`, or `--refresh` | Imports every live Yoma course, assesses all of them at the product's polite pace, grades them, and saves the database | about 45 minutes, projected: alison.com's 2,054 pages at 1.1 s each set the pace |
| `--sample 40` | Imports every live course but assesses only 40 from mixed hosts, and saves nothing. For testing | about 3.5 minutes |

Building the API and website adds about 13 minutes the first time, and a few minutes after code changes.

If a full run stops part way (a laptop sleeps, a site is down), carry on without starting again:

```bash
scripts/course-quality-demo/assess-all.sh
```

It assesses only the live courses not yet done, grades, and saves the database.

To stop it and delete all demo data (the saved database in `.cache/` stays):

```bash
scripts/course-quality-demo/stop.sh
```

If the venue's internet is unreliable, use only the local demo pages for the named courses:

```bash
scripts/course-quality-demo/start.sh --fixtures
```

## What browsing shows

Courses appear as compact learner cards, coloured by category family, with verified courses first
and the most popular first within each group (0.7 x completed + 0.3 x started). The New section is
newest first. Selecting a card pops out the full learner card with fields and example roles.

## Sign-ins

| Role | Email | Password |
| ---- | ----- | -------- |
| Reviewer, and partner admin of Demo Learning Partner | `testadminuser@gmail.com` | `P@ssword12` |
| Partner only | `testorgadminuser@gmail.com` | `P@ssword12` |
| Learner, in the United States | `testuser@gmail.com` | `P@ssword12` |

The reviewer account is also an organisation admin of the demo partner (`c0a7e000-0000-4000-8000-000000000001`), so one
sign-in reaches the learner, reviewer and partner views, and switching between them is only navigation. The partner-only
account still works as before.

These are the public local defaults from `src/api/docker-compose.yml`, not real accounts.

The seed marks these accounts' settings as done, so "Choose your settings" does not appear after
signing in. Signed-out visitors may see a question about analytics. Choose either answer and it
stays dismissed in that browser.

To switch accounts, select your avatar at the top right, scroll to the bottom of the panel, and
choose Log out. Each `start.sh` also signs every browser out of the previous demo.

## What is in the demo

- **Every live Yoma Learning course: 2,401 listings** from the public, signed-out Yoma search, fetched on
  16 November 2026 into `.cache/live-catalogue.json` by `seed/fetch-live-catalogue.mjs`. Ids, titles, descriptions, links,
  organisations (28, with their real names), categories, countries, languages, skills, commitment, dates, verification
  settings, Zlto reward, engagement type and featured flags are as on live Yoma. Each is assessed against its real page.
  14 live listings have no link. They are imported as they are, and the assessment marks them Not verified.
- **The named courses 101 to 108**, which the walkthrough uses, below. Their summaries start with "Demo data".
- **Categories** are matched by their live ids, the way this branch's category migration moves live Yoma to its 16
  categories: eight live categories keep their ids under new names (for example "Business and Entrepreneurship" becomes
  "Business, Finance & Marketing"), "Environment and Climate" joins "Agriculture, Food, Environment and Climate", and
  "AI, Data and Analytics" joins "Technology, AI & Data". No category is added.
- **Organisation logos and contact details** are not imported. Logos stay off, and contact details are placeholders.
- **Not imported**, because this database has no place for it: the live difficulty level.

### Scaled demo data: views, starts and completions

Live counts are real but can be huge (one course has 45,894 participants), and every demo row needs a demo learner.
So the seed adds 50 demo-only learners, who cannot sign in, and gives each live course rows scaled from its real counts,
keeping the ratios between courses:

- started = scaled(the larger of link clicks and participants), at most 40
- completed = started x (real completions / real participants), rounded, and at least 1 if any learner really completed it
- viewed = scaled(views), at most 50, and never fewer than started
- scaled(n) is n up to 5, then grows with the logarithm of n, reaching the cap at 50,000 starts or 25,000 views. 100 real
  starts become 16, 1,000 become 25, and 14,575 become 35.

This is demo data. The formula is in `seed/live.mjs`.

### Synthetic learner feedback

On a fresh import, when the API has the learner feedback table, the seed adds made-up "Did you take it?" answers from the demo learners:
about 300 live courses, 1 to 25 answers each, about 1,500 in all. Ratings loosely follow each course's real completion
ratio, tags use the keys in the API's `CourseQuality:LearnerFeedback` settings, and about 1 in 5 has a short generic comment such as "Easy to follow."
No learner wrote any of it. It is demo data only, generated by `seed/generate-feedback-sql.mjs`.

## The named courses

The address of a learner card is `http://localhost:3000/opportunities/c0a7e000-0000-4000-8000-000000000` followed by the
number. Courses 101 and 105 are copies of live listings, so their titles end in "(walkthrough copy)" to keep titles unique.
For 101 the added words mean its real page no longer matches the listing, so `start.sh` moves it to its demo page. The
live listing it copies is also in the demo, assessed on its real page.

| No. | Course | Shows | Page |
| --- | ------ | ----- | ---- |
| 101 | Bike Safety and Maintenance Essentials (walkthrough copy) | Verified, self-paced | Demo page (real page tried first) |
| 102 | Digital Marketing Foundations Cohort Course | Verified, dates confirmed | Demo page |
| 103 | Introduction to Open Mapping for Climate Action | Not yet verified | Real page |
| 104 | Tomorrow University - Mission Identification | Paid cost | Real page tried first, currently the demo page |
| 105 | Earth Observation for Ecosystem Conservation (walkthrough copy) | Not yet verified, cost not confirmed | Real page |
| 106 | Career Readiness Live Webinar | Webinar | Demo page |
| 107 | Community Health Basics Course | Not verified, with a fix note | Demo page, broken on purpose |
| 108 | Solar Installation Skills Course | Pending review, page dates differ | Demo page |

101 belongs to the live Alison Learning organisation. The others belong to Demo Learning Partner, the only fictional
organisation.

The real page for 103 currently gives "Cost not confirmed". Its demo page, used with `--fixtures`,
gives Free. The real page for 104 no longer gives a paid cost under the final rules, so `start.sh`
moves it to its demo page and says so. The table printed by `start.sh` always shows the actual result.

Free needs schema.org data with price 0 and no visible fee, so the demo pages meant to read Free
carry that data. It is demo data too.

Real pages can change. If one no longer gives the expected result, `start.sh` moves that course to
its demo page, and the table it prints says "demo page". Demo pages carry a banner saying that they
are stand-ins, not a provider's page.

## How the full run treats live sites

The assessment fetches each live course page once, at the product's normal pace: 1.1 s between requests to one host, and
one request at a time per host, with up to 4 hosts in parallel. Most courses are on alison.com, so that host sets the
length of the run. Progress goes to `.cache/assessment-progress.jsonl`, so `assess-all.sh` can resume. The saved database
(`.cache/demo-db.pgdump`) is the whole demo database, compressed. It holds public Yoma data, the demo accounts and the demo
data above, and is git-ignored because it is large.

The demo switches off the hourly background assessment (`CourseQuality__BatchSize=0`) and keeps results current for 10
years, so a demo started weeks after its run still shows its badges. These settings are in `demo.env` and belong to the
demo only.

## Five-minute walkthrough

**1. Browse and learner card (2 minutes).** Stay signed out, or use a private window.

1. Open `http://localhost:3000/opportunities`. Browse the course tiles: verified courses come first, and each tile shows
   status, time and cost. Select one to pop out the full card.
2. Open 101. Point out "Verified by Yoma" and "Start anytime". This badge means live and matching its
   page, not that dates were confirmed.
3. Open 102. Point out "Verified by Yoma" with start and end dates, and "Dates checked" under What
   we checked. The cost shows Free.
4. Open 104. It shows "Not yet verified" and the paid cost, EUR 100, read from the page. Open 105 to
   show "Cost not confirmed — check before you enrol". The product never guesses "free".
5. Open 106. The listing is marked Webinar, not Course.
6. Open 103. "Not yet verified" is factual, and makes no accusation against the partner.

**2. Reviewer queue and coverage (2 minutes).** Sign in as the reviewer and open
`http://localhost:3000/admin/course-quality`.

1. Start with the coverage boxes at the top: Total courses 2,409 (every live course and the 8 named ones), Assessed,
   Missing or suspect dates, Course, Webinar, Unknown and Card ready.
2. Find Solar Installation Skills Course in the Pending list, which is oldest first and, after a full run, holds most of
   the catalogue. On a fresh import the named courses are assessed first, so it is near the top. After a start from the
   saved database they are assessed last, so it is at the end. "Dates are accurate" says the dates on
   the course page differ from the listing, so a person decides. The "Page reader" notice says no AI ran.
3. Choose Mark Not verified. The dialog has a Format picker (Course or Webinar) and a Note. Leave
   Format as Course and choose Save decision. The course leaves Pending and becomes a partner fix note.

**3. Partner fix note (1 minute).** Stay signed in as the reviewer, who is also this partner's admin, and open
`http://localhost:3000/organisations/c0a7e000-0000-4000-8000-000000000001/course-quality`. The partner-only account,
`testorgadminuser@gmail.com`, shows the same page.

1. Community Health Basics shows "Why it is held back" and "Steps to improve its visibility": its link
   returned page not found (404).
2. Solar Installation appears here too, if you resolved it in step 2.
3. A reviewer's note does not appear here, and the page says that notes are not emailed.

## How the demo pages reach the link checker

The link checker refuses private addresses by design, and the demo does not change it. Docker
normally gives containers private addresses, so the demo pages would be refused. Instead, they run
on a closed Docker network created only for this demo. It uses a non-private address range
(`11.66.0.0/24`), with the real checker running unchanged. The network exists only inside Docker,
and `stop.sh` removes it. The links use `localtest.me`, a public name that points to your own Mac,
so a browser can open them too. Opening one needs internet for that name lookup, and the course
assessment does not. This setting belongs to the demo and must not be copied to any deployed
environment.

The API runs in a Linux container, so it can reach sites that accept only TLS 1.3, which the .NET
runtime on macOS cannot.

## Troubleshooting

- **Something failed:** run `stop.sh`, then `start.sh`. Logs are in `scripts/course-quality-demo/.logs/`.
- **A full run stopped part way:** run `assess-all.sh` while the demo is running. It carries on from
  `.cache/assessment-progress.jsonl`, then grades and saves the database.
- **The saved database is from older code:** run `start.sh --refresh`. A saved database loads into newer code (its
  migrations run on start), but its results were made by the older rules.
- **"src/web/Dockerfile changed shape":** `start.sh` copies that Dockerfile into `.cache/web.Dockerfile` and adds the
  demo's build-time switches after its `ENV NEXT_PUBLIC_GA_MEASUREMENT_ID=` line. If that line moves, update the `awk`
  step in `start.sh`, or add `ARG`s for `NEXT_PUBLIC_COURSE_QUALITY_DEMO` and `NEXT_PUBLIC_COURSE_QUALITY_DEMO_ORG_ID` to
  `src/web/Dockerfile`.
- **A row says "NOT AS EXPECTED":** check that course before presenting it. The summary lists only the named courses and
  any that missed.
- **Port in use:** the message names the app. Quit it and start again.
