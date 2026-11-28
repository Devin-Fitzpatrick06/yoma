# AI co-author in the partner portal: mock-up

New-ideas section, owner Jongmin. A **mock-up, not a build**: no AI calls, no backend, fictional data.

A partner types one line ("Green jobs fair at our hub on 12 Dec…"). Yoma drafts every step of the real
8-step opportunity form, flags what the AI added, asks for the facts it can't know, runs Ziqi's
pre-publish check, and the partner publishes after an explicit sign-off.

| File | What |
| --- | --- |
| `index.html` | Clickable mock-up, 4 screens, each with a design-notes rail (numbered pins) |
| `screens/s1.png` … `s4.png` | 2× renders for the slides (1760 × ~1000, notes rail included) |

## Open it

**Live, clickable (private link, share it from the page's Share menu):**
https://claude.ai/artifact/6bqfNgL43ErazSq1MFHqMg. It scales to fit any screen, including a phone.

Locally:
double-click `index.html`, or serve the folder:

```bash
python3 -m http.server 8080 --directory docs/work/active/course-quality-judger/co-author-mockup
```

Then open `http://localhost:8080/`. Views:

- `#s1` to `#s4`: one screen. The buttons inside each screen click through the flow; ← → keys also work.
- `?board`: all four screens side by side (use this for Figma).
- `?notes=0`: hide the design-notes rail for clean screens (use with `?board` for clean Figma frames).

## Get it into Figma

Figma can't open HTML directly; it imports it with the **html.to.design** plugin (free tier is enough for
4 screens). The page is built for that importer: flexbox only, real text, Google Fonts (Nunito, Open Sans,
both in Figma), no images, no fixed or sticky layers. Each screen is one frame (`.frame`, 1760 px wide with
notes, 1440 px without).

1. Install the html.to.design Chrome extension and Figma plugin.
2. Serve the folder (command above) and open `http://localhost:8080/?board&notes=0` in Chrome.
3. Use the extension to capture the page, then import the file in the Figma plugin. Viewport 1440 for clean
   frames.
4. Repeat with `?board` (notes on) if you want the annotated versions for the walkthrough slide.

Not yet tried in Figma from this session (no Figma access here); check that the purple header band
imports as one rectangle.

## Data used

All from `scripts/course-quality-demo/journey/shared-mock-data.json`:

- `newIdeas.coAuthor`: the partner's input, the drafted title, description and skills, and the
  `missingInfo` questions.
- `org-arewahub` (Arewa Tech Hub) and `opp-event-001` (Kaduna Youth Green Jobs Fair, 20 ZLTO).

Colours are Yoma's own tokens (`src/web/src/styles/colors.css`). Steps, field names and categories match
the real form (`src/web/src/pages/organisations/[id]/opportunities/[opportunityId]/index.tsx`) and the
seeded categories.

## Decisions

- **Event, not the solar job.** The shared data gives the co-author scenario to Arewa Tech Hub's jobs fair.
  The solar job (`opp-job-001`) is created with RoleMap, Anagha's employer role tool, which the data calls
  "the co-author's job-listing mode". Using the event keeps the two ideas from overlapping on stage.
- **AI additions are flagged, not hidden.** The shared draft adds a "20-minute CV clinic" and "ages 18 to
  35", neither of which is in the partner's note. Rather than quietly fix the data, the mock-up uses this as
  the core safeguard: each addition must be kept or removed by the partner.
- **Never drafted:** ZLTO reward, verification method, credential. They carry money and trust.
- **Paired with Ziqi.** The pre-publish check already exists on the real form; the co-author only adds the
  drafting step in front of it. Events show a status, not a letter grade (grades are for courses and jobs).
- **Standalone HTML, not built into the real partner page.** See Open items.

## Open items

- **Liquid Thought's portal wireframes.** GenU's AI use-case note says Liquid Thought has already drawn this
  concept. Ask GenU for them before the final deck, so the mock-up doesn't contradict their designer.
- **Cost of not having it.** The "≈ 6 min" on screen 4 is illustrative. Needs real minutes per listing and
  listings per month from GenU.
- **Design brief (1–2 pages) and 2 slides:** not started.
