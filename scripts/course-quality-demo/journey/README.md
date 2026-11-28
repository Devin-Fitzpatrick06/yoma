# Connected journey: the shared mock data

**Everything in `shared-mock-data.json` is fictional.** Halima Yusuf, Sunrise Solar Ltd, every listing, id, YoID and
link is made up for the BBS prototypes. None of it is real Yoma, YoID or partner data.

- **Source:** a verbatim copy of `mockups/shared-mock-data.json` from `anaghataneja-stack/pathfinder-prototype` at
  commit `69d9f57` (the file itself last changed there in `081beae`). Version 1.1, updated 2026-11-24. These SHAs are in
  that repository, not this one.
- **Do not edit this copy.** It is the same file PathFinder and SkillLens carry. A change goes into the shared file
  first, then is copied here again.
- **How Ziqi uses it:** `start.sh --journey` turns its 5 organisations and 9 opportunities into Yoma listings
  (`seed/generate-journey-sql.mjs`), serves a fictional page for each one at
  `http://demo-fixtures.localtest.me/journey/<shared id>` (`fixtures/server.mjs`), creates Halima's local sign-in
  (`seed/journey-user.sh`) and runs Ziqi's checks on the 9 listings.
- **Ids:** the mapping from the shared ids to the Yoma opportunity and organisation ids is in
  `docs/work/active/course-quality-judger/journey-contract.md`, "Ziqi's data mapping".

## What Ziqi adds that the shared file does not say

Yoma needs a few fields the shared file leaves out. These are Ziqi's own choices, not part of the contract:

| Listing | Added |
| --- | --- |
| Every listing | Country Nigeria, a category from Yoma's list, and the listing's languages that Yoma has (English, Hausa, Yoruba; Nigerian Pidgin has no Yoma language) |
| `opp-job-001`, `opp-job-002` | Listed 10 days before the demo date; `opp-job-001` closes on its deadline, 15 January 2027, and says "full time" (its hours are Mon to Sat, 8am to 4pm) |
| `opp-job-002` | A description in which the ₦5,000 registration fee is asked for, as in its title; pay is commission only |
| Courses 001 to 005 | Listed 30 days before the demo date, no end date, commitment = the course's hours, free (schema.org price 0). 001 to 004 are self-paced online; 005 is in person, and its page returns 404 |
| Courses 001 to 004 | Three or four learning outcomes and a three-module outline on the page, as a complete provider page has |
| Sample organisations | Kaduna, Nigeria; placeholder contact details; the self-paced courses' organisation, "Passport to Earning (sample courses)", is a self-paced provider on the demo pages only (`demo.env`) |
| `opp-event-001` | 10:00 to 16:00 on 12 December 2026 (6 hours) |
| `opp-impact-001` | 08:00 to 12:00 on 29 November 2026 (4 hours). After that date the listing has ended, and Ziqi says so |
| Skills | Only existing Yoma skills, matched by name to the shared skill keys; keys with no Yoma skill are left out |
