# Course Quality demo

Runs the Course Quality prototype on this Mac. Everything uses placeholder settings, and the
assessment is switched on through the demo's environment, so the product's own configuration does
not change.

**This demo is for Yoma staff only. Do not show it to learners or partners as a live service.** The
badges and costs are provisional: the scoring criteria are placeholders until the project lead sets
the final ones, and no AI ran. "Verified by Yoma" here means the current rules passed, and a cost is
only what the page reader found. Treat every result as a prototype output. The AI check is off unless you start with
`--local-ai` (a free open model on this Mac) or point it at another server; see "Ziqi AI" below.

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

When it finishes, it prints the links (including the Ziqi iteration 4 and 5 pages, below), the test sign-ins, the counts
by type (courses, events, impact actions, jobs), whether Ziqi's AI is on (provider, model, local and free or not) and a
summary. Open the course list only after that:
the website starts last, once every course is in, and `start.sh` loads the course list once so the page it keeps for
5 minutes already shows the courses.

What a start does depends on whether a saved demo database is in `scripts/course-quality-demo/.cache/`:

| Start | What happens | Time on this Mac |
| ----- | ------------ | ---------------- |
| Saved database present | Loads it, sets the named courses 101 to 108 to today's dates, assesses those 8 again and regrades | about 2.5 minutes |
| No saved database (the default) | Imports every live Yoma course, event, impact action and job, assesses and grades a sample of 40 courses, opens the website. Saves nothing | about 8 minutes |
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
- **Not imported**, because this database has no place for it: the live difficulty level of courses.
- **Every live Yoma impact action: 41 listings** (27 November 2026), fetched into `.cache/live-tasks.json` by
  `seed/fetch-live-catalogue.mjs --tasks` and added by `seed/generate-tasks-sql.mjs`. Live Yoma's public type list calls
  them "Task" (`f12a9d90-a8f6-4914-8ca5-6acf209f7312`). This branch's custom-field migration renames that row in place to
  `ImpactAction` ("Impact Action") and keeps the id, so they are imported under the local name `ImpactAction`. Categories
  (by live id, as for courses), countries, languages and skills come with them. The live difficulty goes into the
  `ImpactActionDifficulty` custom field (Beginner and Any Level become Entry level, Intermediate becomes Experience needed,
  Advanced becomes Skills required, as the migration maps them). The 3 tasks pulled from IXO get a partner sync record,
  as feed jobs do. A task whose title is already taken is skipped. `start.sh` adds them once, before the jobs, only when
  the database has no impact actions, so an older saved database gets them on its next start (and is saved again). Then
  Ziqi's listing checks run on them (`seed/assess-live.mjs --tasks --no-grade`); impact actions get no grade.
- **Every live Yoma event: 17 listings** (28 November 2026), fetched into `.cache/live-events.json` by
  `seed/fetch-live-catalogue.mjs --events` and added by `seed/generate-events-sql.mjs`. The type is `Event`
  (`e20dcceb-59c3-46d0-ab0a-d321d8bc4c31`) on live Yoma and in this branch alike. Categories, countries, languages and
  skills come with them, as for impact actions; the live difficulty goes into the `EventDifficulty` custom field (Beginner
  and Any Level become Open to all, Intermediate becomes Familiarity needed, Advanced becomes Experienced individuals, as
  the custom-field migration maps them). 10 of the 17 have no link, so their "link works" check fails: that is live data,
  not a demo fault. No participation is added for events. `start.sh` adds them once, after the impact actions, only when
  the database has no events (so an older saved database gets them and is saved again), then runs Ziqi's listing checks
  on them (`seed/assess-live.mjs --events --no-grade`, `CourseQuality__VerifyTypes__1=Event`); events get no grade. With
  courses, impact actions and jobs, the demo audits all four types.

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

### Synthetic pathway completions: "usually go on to"

The live impact actions bring no participation. So that My Yoma's "young people who completed this usually go on to …"
can show (it needs at least 20 youth per step), `seed/generate-tasks-sql.mjs` adds one small chain per demo goal
(`Pathway:Goals` from the API's `appsettings.json`):

- **A**, a course of the goal's stages (not the last) with the most demo completers more than 45 days ago, at least 21;
- **B**, the impact action most plausibly next: it matches one of the goal's stage patterns, shares skills with A, or
  shares its category. Each goal prefers one not used by another goal;
- **C**, a course of the next stage after A, preferring one that shares skills or a category with A or B.

24 of A's completers (picked by a hash of their email, so every run picks the same) complete B 6 to 12 days ago, and 16
of those complete C 2 to 5 days after B. Recent dates keep other completions from crowding out C as B's "next". On the
27 November catalogue all six goals get a chain: about 240 rows. Only completions are added, never a start without one,
so no drop-off is implied. Each row's `CommentVerification` says "Demo data: a synthetic pathway completion by a demo
learner", and My Yoma labels every figure from the demo learners (`Pathway__DemoDataNote` in `demo.env`). The generator
adds the chains once: a database that has them already is left as it is. `.logs/seed-tasks.log` lists the chains.

### Synthetic learner feedback

On a fresh import, when the API has the learner feedback table, the seed adds made-up "Did you take it?" answers from the demo learners:
about 300 live courses, 1 to 25 answers each, about 1,500 in all. Ratings loosely follow each course's real completion
ratio, tags use the keys in the API's `CourseQuality:LearnerFeedback` settings, and about 1 in 5 has a short generic comment such as "Easy to follow."
No learner wrote any of it. It is demo data only, generated by `seed/generate-feedback-sql.mjs`.

## Ziqi iteration 4: pages and switches

These switches are on in `demo.env` only; they are off by default everywhere else, and in production.

| Switch | What it turns on |
| --- | --- |
| `Pathway__Enabled`, `NEXT_PUBLIC_PATHWAY_ENABLED` | My Yoma: questions, the journey map, lock and swap, step events; the YoID "My path" card |
| `CourseQuality__PrePublish__Enabled`, `NEXT_PUBLIC_PREPUBLISH_ENABLED` | Ziqi's check on the partner's create and edit form |
| `CourseQuality__VerifyTypes__2=ImpactAction` | Listing checks on impact actions |
| `Pathway__DemoDataNote` | The "Demo data" note on every figure from the synthetic learners |

`start.sh` passes the two `NEXT_PUBLIC_` switches to the website build. The pages it lists at the end:

| Page | Address |
| --- | --- |
| My Yoma (youth) | `http://localhost:3000/my-yoma` |
| YoID, My path card | `http://localhost:3000/yoid` |
| Partner, new listing with the Ziqi check | `http://localhost:3000/organisations/c0a7e000-0000-4000-8000-000000000001/opportunities/create` |
| Journey map preview (fixtures, no sign-in) | `http://localhost:3000/my-yoma-preview?view=map` |
| Ziqi draft preview | `http://localhost:3000/ziqi-draft-preview` |
| Ziqi admin preview | `http://localhost:3000/ziqi-admin-preview` |

The five-step script for these, with what to say and the caveats, is
`docs/work/active/course-quality-judger/demo-walkthrough.md`.

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

## Ziqi AI (iteration 5): a free, local open model

The AI check is off by default, and every check `start.sh` runs at start-up runs without it, so a start takes the same
time either way. With one of the options below, the API restarts with the AI on at the end of the start. Then a
reviewer runs it on demand: **Run AI check** on a course's reviewer card. No paid AI is used unless you point the demo at
a paid server yourself.

| Start | Where the model runs | Cost |
| --- | --- | --- |
| `start.sh --local-ai` | An Ollama container in the demo (`ollama/ollama:0.12.3`), model `qwen2.5:3b` | Free: every run records $0.00 |
| `start.sh --local-ai-model qwen2.5:1.5b` | The same, a smaller model: faster, weaker answers | Free |
| `start.sh --local-ai-url http://host.docker.internal:11434/v1` | The native Ollama app on this Mac (uses the Mac's GPU through Metal, much faster) | Free |
| `start.sh --ai-url URL --ai-model NAME` | Any OpenAI-compatible server, for example a government host | Whatever that host charges; shown only when `AI:Prices` has the model, otherwise "unknown", never guessed |

- **The first `--local-ai` start downloads the model** (about 2 GB for `qwen2.5:3b`, 1 GB for `qwen2.5:1.5b`) while the
  API and website build. It is kept in the Docker volume `yoma-course-quality-demo-ollama-models`, which `stop.sh` keeps;
  remove it with `docker volume rm yoma-course-quality-demo-ollama-models`. Give Docker about 8 GB of memory.
- **Speed on a Mac:** Docker Desktop has no GPU access, so the container runs the model on the CPU. One course check
  with `qwen2.5:3b` takes roughly 1 to 3 minutes on an Apple-silicon Mac, most of it reading the page text (the first
  after a start also loads the model, about 10 to 20 seconds more), and `qwen2.5:1.5b` about half that. These are
  estimates from the model size and prompt length, not yet timed on a Mac: `ai-smoke.sh` prints the real duration. The API waits up to 300 seconds. For a live
  audience, the native Ollama app is much faster: install it from ollama.com, run `ollama pull qwen2.5:3b`, then start
  with `--local-ai-url http://host.docker.internal:11434/v1`. On Linux the app must listen beyond localhost
  (`OLLAMA_HOST=0.0.0.0 ollama serve`).
- **A key, if the server needs one,** is read from the environment variable `ZIQI_AI_API_KEY` in the shell that runs
  `start.sh` (for example `ZIQI_AI_API_KEY=... scripts/course-quality-demo/start.sh --ai-url ... --ai-model ...`). It is
  passed to the API container as `AI__OpenAICompatible__ApiKey` at runtime only and never written to a file.
- **The settings** go to the API through its environment (`docker-compose.demo.yml`, service `api`, names only):
  `AI__Provider=OpenAICompatible`, `AI__OpenAICompatible__BaseUrl` (`http://ollama:11434/v1` for the container),
  `AI__OpenAICompatible__Model`, `AI__OpenAICompatible__IsLocal` (true for both local options, so the cost is 0) and
  `AI__OpenAICompatible__TimeoutInSeconds=300`. Without an AI option, `AI__Provider=None`.
- **The model server is internal:** no port is published. The API reaches it as `ollama`, by service name on the demo's
  default network, which the gateway (whose network the API shares) is on.

One-command proof that a live LLM runs through the backend, with its tokens and cost:

```bash
scripts/course-quality-demo/ai-smoke.sh
```

It signs in as the demo admin, runs `POST /courseQuality/opportunity/{id}/assess` on named course 102 (or the number
or id you give it) and prints the model, provider, tokens in and out, cost, duration and outcome. The AI can only lower
a result, so the course may need a look afterwards; the next `start.sh` resets the named courses.

## Link health (iteration 5)

- In the website: `http://localhost:3000/admin/course-quality/links`, signed in as the reviewer. Paste or upload links
  from any platform (Yoma, YouthHub, 1MiO), run, read the results and export a CSV.
- Without Yoma: `node scripts/link-health/check-links.mjs` reads a .txt, .csv or sitemap and writes a CSV or Markdown
  report. It needs only Node; see `scripts/link-health/` for its options.

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
- **`ai-smoke.sh` says the AI is off:** the demo was started without an AI option. Run `stop.sh`, then
  `start.sh --local-ai`.
- **The AI check times out or the API log shows a timeout:** the CPU model is too slow for that page. Use
  `--local-ai-model qwen2.5:1.5b`, or the native Ollama app with `--local-ai-url`.
- **The model download failed:** see `.logs/local-ai.log`, and check the model name on ollama.com/library.
