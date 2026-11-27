# Iteration 4: teams, engineers and handoffs

Status: in-progress

Plan: `iteration-4.md` (W0–W7). Lead: the integrating session. Base commit for every engineer: the commit that adds this
file. Every engineer works in its own worktree and branch, commits there, never pushes; the lead merges.

## Shared contracts (lead only; ask the lead to change them)

- Web ↔ API: `src/web/src/api/models/pathway.ts` and the "Iteration 4" block of `src/web/src/api/models/courseQuality.ts`.
  The API mirrors them field for field (camelCase JSON).
- Web services, already written: `src/web/src/api/services/pathway.ts` (iteration 4 block),
  `src/web/src/api/services/courseQualityDraft.ts`.
- Map ↔ page: `src/web/src/components/MyYoma/JourneyMap/types.ts` (`JourneyMapProps`).
- Engine ↔ step store: `Yoma.Core.Domain.Pathway.Interfaces.IPathwayStepStore` (no-op default registered).
- Switches: web `NEXT_PUBLIC_PATHWAY_ENABLED`, `NEXT_PUBLIC_PREPUBLISH_ENABLED` (in `src/env.mjs`, on in the demo
  build); API `Pathway:Enabled`, `CourseQuality:PrePublish:Enabled` (on in `demo.env`; the API options are A1's).

## Teams

### Team API (Adrian's area): four engineers

| Engineer | Owns (only these files) | Builds |
| --- | --- | --- |
| **A1 Platform & badges** | `Pathway/Models/PathwayOptions.cs` (Enabled only), `Pathway/Badges/**`, `PathwayBadgeController`, `CourseQuality/Models/CourseQualityOptions.cs` (PrePublish section only), `appsettings.json` | W0: `Pathway:Enabled` gates every `/pathway/*` endpoint (404 when off); `CourseQuality:PrePublish` options class. W2: Platinum level and thresholds, `reviewDue`, a batched `GET /pathway/badges/organizations?ids=` for course cards |
| **A2 Ziqi checks** | `CourseQuality/**` except the options file section above, `CourseQualityController` | W1: `POST courseQuality/organization/{id}/draft/check` reusing the rule engine without storing; criterion → form-field mapping; partner wording; link check with a short timeout that never hard-fails. W4: Impact Action listing checks (`VerifyTypes`). "Draft this for me" suggestions through the AI provider when configured, else `null` |
| **A3 Pathway engine** | `Pathway/Services/PathwayEngine.cs`, `PathwayService.cs`, `PathwayCatalogueService.cs`, `Pathway/Helpers/**`, `Pathway/Models/PathwayDefaults.cs`, `PathwayContract.cs`, `PathwayController.cs` (existing endpoints) | W3: read `UserPreferences`, `answeredFromPreferences`, ask only what is missing, save answers back to Yoma preferences where a field exists. W4: step types per stage (Learning, ImpactAction, Task) in goals config, impact candidates, `usuallyNext`, `partnerBadge`, `dropOff` = Collecting, `startLabel` / `destinationLabel`; locked and swapped steps from `IPathwayStepStore` |
| **A4 Steps & data** | new `PathwayStepService`, `PathwayStepController` (`/pathway/me/steps/*`, `/pathway/me/events`, `/pathway/health`, `/pathway/yoid/me`), the stored `IPathwayStepStore`, the **only** migration (`ApplicationDb_Pathway_Steps`), its entities and repositories; `CourseQualityGradingDefaults` `DropOff` factor at weight 0 | W5 and the lock/swap/alternatives/YoID endpoints |

### Team Web (Jason's area): four engineers

| Engineer | Owns | Builds |
| --- | --- | --- |
| **W1 Journey map** | `components/MyYoma/JourneyMap/**` (except `types.ts`), a `view=map` case in `pages/my-yoma-preview.tsx` | W7 map: Start → stages → First job, path line that draws itself, cards that fly in and lock, step-type icons, grade and partner badge, `usuallyNext`, drop-off slot, lock and swap sheet, "You are here", celebration; reduced motion; 320 px |
| **W2 First visit** | `pages/my-yoma/index.tsx`, `components/MyYoma/` except `JourneyMap/` and `PartnerBadgeCard.tsx`, `pages/index.tsx` (redirect only), `components/NavBar/Navbar.tsx` | W0 web switch (nav item and page 404 when off); fly-in YoID card; question slides with popping chips, only the questions not answered from preferences; "building your path"; first visit after sign-in lands on My Yoma; renders `JourneyMap` with `JourneyMapProps` |
| **W3 Partner portal** | `pages/organisations/[id]/opportunities/[opportunityId]/**`, new `components/CourseQuality/DraftCheckPanel*`, `components/MyYoma/PartnerBadgeCard.tsx`, `components/Opportunity/**`, `pages/organisations/[id]/index.tsx` and `info.tsx` | W1 panel on the create/edit form (switch `NEXT_PUBLIC_PREPUBLISH_ENABLED`); W2 badges on public surfaces incl. Platinum; "Ziqi" labels on partner and learner surfaces |
| **W4 YoID & admin** | `pages/yoid/index.tsx`, new `components/YoID/MyPathCard.tsx`, `pages/admin/course-quality/**` | W3 "My path" card on YoID; W5 pathway health panel and drop-off slot on the reviewer card; "Ziqi" labels on admin pages |

### Team Data & Demo: one engineer

| Engineer | Owns | Builds |
| --- | --- | --- |
| **D1 Data & demo** | `scripts/course-quality-demo/**`, `docs/work/active/course-quality-judger/data-mapping.md`, `demo-walkthrough.md` | Import live Impact Action / Task listings; synthetic, labelled demo completions on them so `usuallyNext` shows; `start.sh` prints the My Yoma link and counts by type; W6 mapping table (Ziqi fields → custom fields and credential schema attributes); the 5-step walkthrough |

## Rules for every engineer

- Read `AGENTS.md`, the component `AGENTS.md`, `iteration-4.md`, this file and `handoffs/2026-11-27-a.md` first.
- Touch only the files you own. Need a change elsewhere? Write it in your handoff under "Requests"; the lead does it.
- No new npm or NuGet dependencies. No `helm/`, `.github/`, `env.secrets`.
- Off by default outside the demo. Nothing invented is shown as real: demo data is labelled; drop-off shows
  "Collecting from the pilot".
- Web without the API: use the fixtures (`components/MyYoma/myYoma.fixtures.ts` and your own) on the preview page.
- Checks before your last commit. API: `dotnet build` (0 warnings), tests for your area
  (`dotnet run --project src/test/Yoma.Core.Test -- -namespace "Yoma.Core.Test.<Area>*"`), `dotnet format
  --verify-no-changes`. Web: `npx tsc --noEmit`, `pnpm lint`, `pnpm test`.
- **Handoff, mandatory:** `docs/work/active/course-quality-judger/handoffs/2026-11-27-<engineer>.md` (e.g.
  `2026-11-27-a3.md`): built, verified (with numbers), not verified, requests to the lead, open questions.
- If you run out of usage, commit what builds, write the handoff with where you stopped, and stop.

## Merge order (lead)

A1 → A2 → A4 → A3 (engine last, to wire the step store) → D1 → W2 → W1 → W3 → W4, then one integrated demo run,
a reviewer pass and a tester pass on the whole batch, one fix round, and the iteration handoff.
