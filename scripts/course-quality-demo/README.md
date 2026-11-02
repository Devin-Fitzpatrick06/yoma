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

The first start builds the API and website, and took about 13 minutes on this Mac. Later starts
took 1 to 2 minutes, or about 6 if much of the code had changed. When it finishes, it prints the links, the test sign-ins, and a summary
of the 58 demo courses. Wait for this before opening the course list, because the page keeps what it first loaded for 5 minutes.

To stop it and delete all demo data:

```bash
scripts/course-quality-demo/stop.sh
```

Running `start.sh` again always gives you a fresh demo.

If the venue's internet is unreliable, use only the local demo pages:

```bash
scripts/course-quality-demo/start.sh --fixtures
```

## Sign-ins

| Role | Email | Password |
| ---- | ----- | -------- |
| Reviewer | `testadminuser@gmail.com` | `P@ssword12` |
| Partner | `testorgadminuser@gmail.com` | `P@ssword12` |
| Learner, in the United States | `testuser@gmail.com` | `P@ssword12` |

These are the public local defaults from `src/api/docker-compose.yml`, not real accounts.

The seed marks these accounts' settings as done, so "Choose your settings" does not appear after
signing in. Signed-out visitors may see a question about analytics. Choose either answer and it
stays dismissed in that browser.

To switch accounts, select your avatar at the top right, scroll to the bottom of the panel, and
choose Log out. Each `start.sh` also signs every browser out of the previous demo.

## The demo courses

There are 58 Learning courses: the 8 named below, which the walkthrough uses, and 50 in the catalogue. Each listing's
summary starts with "Demo data". The address of a learner card is
`http://localhost:3000/opportunities/c0a7e000-0000-4000-8000-000000000` followed by the number.

| No. | Course | Shows | Page |
| --- | ------ | ----- | ---- |
| 101 | Bike Safety and Maintenance Essentials | Verified, self-paced | Real page |
| 102 | Digital Marketing Foundations Cohort Course | Verified, dates confirmed | Demo page |
| 103 | Introduction to Open Mapping for Climate Action | Not yet verified | Real page |
| 104 | Tomorrow University - Mission Identification | Paid cost | Real page tried first, currently the demo page |
| 105 | Earth Observation for Ecosystem Conservation | Not yet verified, cost not confirmed | Real page |
| 106 | Career Readiness Live Webinar | Webinar | Demo page |
| 107 | Community Health Basics Course | Not verified, with a fix note | Demo page, broken on purpose |
| 108 | Solar Installation Skills Course | Pending review, page dates differ | Demo page |

The real page for 103 currently gives "Cost not confirmed". Its demo page, used with `--fixtures`,
gives Free. The real page for 104 no longer gives a paid cost under the final rules, so `start.sh`
moves it to its demo page and says so. The table printed by `start.sh` always shows the actual result.

Free needs schema.org data with price 0 and no visible fee, so the demo pages meant to read Free
carry that data. It is demo data too.

Real pages can change. If one no longer gives the expected result, `start.sh` moves that course to
its demo page, and the table it prints says "demo page". Demo pages carry a banner saying that they
are stand-ins, not a provider's page. In a real-listing sample, no page confirmed its listing's
dates, so 102 uses a demo page. No real webinar was in that sample either, so 106 does too.

## The catalogue

Courses 109 to 158 fill the browse page, so that it looks like real Yoma. Every one uses a demo page and is demo data.
Providers are the Alison demo organisation and fictional organisations with "Demo" in their names.

- **Categories:** all 16 have at least 3 courses. Technology, Agriculture and Business have 5 or 6.
- **Expected results across all 58:** 35 Verified, 20 Not yet verified, 3 Not verified. 3 are paid and 5 are webinars.
- **Sections:** 9 courses are Featured. 44 are Worldwide, 8 United States and 6 South Africa.
- **Trending & Popular:** 12 courses have views and completions from 10 demo-only learners, who cannot sign in. The section
  ranks by completions, with verified courses first.

To change a course, edit `seed/catalogue.json`. The named courses are in `seed/demo-courses.json`. Both are read through
`seed/courses.mjs`, which builds each page from a pattern that copies a named course. If you add one, keep to these rules:

- **Titles:** no list words such as courses, classes, series, tracks, paths, topics or library, because the page then
  reads as a catalogue. Titles must be unique.
- **Verified with dates:** the page needs labelled start and end dates that match the listing, and an end date still ahead.
- **Verified, self-paced:** the Alison organisation, a start date in the past, no end date, and no dates, cohorts or
  schedule words on the page.
- **Free:** needs the schema.org data, and no digits beside fee or cost wording.
- **Any page:** no wording that it is closed, full or over, and no words such as "registration" or "deadline" just before
  a date.

The link checker waits between requests to one host, and almost every page is on one host, so `demo.env` shortens that
wait to 100 ms. This belongs to the demo only.

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

1. Start with the coverage boxes at the top: Total courses 58, Assessed 58, Missing or suspect dates,
   Course, Webinar, Unknown and Card ready.
2. Find Solar Installation Skills Course near the top of the Pending list, which has about 20 courses, oldest first. "Dates are accurate" says the dates on
   the course page differ from the listing, so a person decides. The "Page reader" notice says no AI ran.
3. Choose Mark Not verified. The dialog has a Format picker (Course or Webinar) and a Note. Leave
   Format as Course and choose Save decision. The course leaves Pending and becomes a partner fix note.

**3. Partner fix note (1 minute).** Sign out, sign in as the partner, and open
`http://localhost:3000/organisations/c0a7e000-0000-4000-8000-000000000001/course-quality`.

1. Three courses are listed. Community Health Basics shows "Why it is held back" and "Steps to improve its visibility": its link
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
- **A row says "NOT AS EXPECTED":** check that course before presenting it. The summary lists only the named courses and
  any that missed.
- **Port in use:** the message names the app. Quit it and start again.
