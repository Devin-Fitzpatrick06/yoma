# Ziqi data mapping (W6)

Status: in-progress — the current mapping is complete. The v5 column waits for Yoma's credential schemas v5.

This table lists every field Ziqi reads or writes: course checks, job facts, pathway inputs, outcomes and badges. For each
one it gives where the value comes from today and where it should come from. W1 (pre-publish check) and W3 (pathway
preferences, the milestone credential) use it. Code names are as of base `cfa9dc0`.

How to read the columns:

- **Opportunity / record**: a column or relation of `Opportunity.Opportunity` (or `MyOpportunity`, `User`,
  `UserPreferences`), as the domain model exposes it.
- **Custom field**: a `Core.CustomFieldDefinition` key (`CustomFieldConstants`, seeded by
  `20260922063352_ApplicationDb_CF_Configuration_Seeding_*`). The value is read from `Opportunity.CustomFields[].ValueRaw`
  or `Core.CustomFieldValue`.
- **SSI attribute (now)**: the attribute in the current default JWS schema per opportunity type, seeded by
  `SSIBackgroundService.SeedSchemas()`, or the YoID ACR schema. "—" means no current attribute carries it.
- **v5 schema**: "pending from Yoma" until Yoma sends the agreed schemas v5.
- **Ziqi reads**: **today** means the code reads this source now. **should** means it ought to read this source but
  reads another one (named in Notes). **n/a** means Ziqi computes the value itself; nothing stores it as a field.

## 1. Course checks (`CourseQualityRules`, `CourseQualityCriterion`)

| Ziqi field | Opportunity / record | Custom field | SSI attribute (now) | v5 schema | Ziqi reads | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| ListingLive: status | `Status` (Active) | — | — | pending from Yoma | today | |
| ListingLive: organisation active | `OrganizationStatus` | — | — | pending from Yoma | today | |
| ListingLive: hidden | `Hidden` | — | — | pending from Yoma | today | |
| ListingLive / DatesAccurate: end date | `DateEnd` | — | — | pending from Yoma | today | |
| DatesAccurate / DataComplete: start date | `DateStart` | — | — | pending from Yoma | today | Compared with the dates on the course page |
| DataComplete: time commitment | `CommitmentInterval`, `CommitmentIntervalCount` (`CommitmentIntervalTotalHours`) | — | `MyOpportunity_CommitmentIntervalDescription` (learner's own) | pending from Yoma | today | |
| DataComplete: timeline (events) | the listing's timeline summary | — | — | pending from Yoma | today | |
| DataComplete: location (jobs) | `Countries` | — | `MyOpportunity_Location` (learner's own) | pending from Yoma | today | |
| LinkWorks | `URL` | — | — | pending from Yoma | today | The page is fetched; a feed job skips the link check |
| FormatIdentified: Course / Webinar | page text and schema.org data | — | — | pending from Yoma | n/a | Detected, not stored on the listing. A "format" field would let partners state it |
| Cost (Free / Paid / Unknown) | page text and schema.org `offers` | — | — | pending from Yoma | n/a | **should**: a cost custom field, once Yoma adds one. No cost field exists today |
| Self-paced | page text | — | — | pending from Yoma | n/a | **should**: a self-paced flag field, once one exists |
| Type rules (`VerifyTypes`: Event, ImpactAction) | `Type` | — | `Opportunity_Type` | pending from Yoma | today | Impact actions get listing checks only, no grade (W4) |

## 2. Course grade factors (`CourseQualityGradingDefaults`, `CourseGradeInput`)

| Ziqi field | Opportunity / record | Custom field | SSI attribute (now) | v5 schema | Ziqi reads | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| SyllabusAvailable, Comprehensiveness | page evidence; else `Description` | — | `Opportunity_Summary` (summary only) | pending from Yoma | today | |
| Comprehensiveness: skills | `Skills` (count) | — | `Opportunity_Skills` | pending from Yoma | today | |
| Accessibility: languages | `Languages` | — | — | pending from Yoma | today | |
| CourseLength | `CommitmentInterval`, `CommitmentIntervalCount` | — | `MyOpportunity_CommitmentIntervalDescription` | pending from Yoma | today | |
| Difficulty (paths and cards) | — | `LearningDifficulty` (Beginner, Intermediate, Advanced; also `Any Level`) | — (a hidden legacy projection only) | pending from Yoma | today | The pathway catalogue reads it. The demo's course import does not set it (see Gotchas in the d1 handoff) |
| Completion | `MyOpportunity` rows: Verification + Completed, NavigatedExternalLink | — | — | pending from Yoma | today | |
| LearnerFeedback | `CourseQuality.LearnerFeedback` | — | — | pending from Yoma | today | Ziqi's own table |
| ProviderReliability | other assessments of the same organisation | — | `Opportunity_OrganizationName` | pending from Yoma | n/a | |
| ListingIntegrity | latest assessment, `DateCreated` | — | — | pending from Yoma | n/a | |
| Outcomes (weight 0) | see section 5 | — | — | pending from Yoma | n/a | |
| DropOff (weight 0, W5) | `PathwayStepEvent` (A4) | — | — | pending from Yoma | n/a | Neutral until the pilot has data; never invented |

## 3. Job facts (`CourseQualityJobRules.Facts`, job grade factors)

"Custom field first, text second" is already in place for jobs. The partner feeds fill the custom fields: JobJack
(`JobJackClient`) and Jobberman (`JobbermanClient`).

| Ziqi field | Opportunity / record | Custom field | Filled by feed | SSI attribute (now, Job schema) | v5 schema | Ziqi reads | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| PayAmount | text: pay regex | `JobSalaryMinimum`, `JobSalaryDisclosed` (+ `JobSalaryMaximum`, `JobSalaryCurrency`) | JobJack: disclosed, min, max. Jobberman: disclosed, min, max, currency | `Opportunity_JobSalaryDisclosed`, `_JobSalaryMinimum`, `_JobSalaryMaximum`, `_JobSalaryCurrency` | pending from Yoma | today (CF first) | Maximum and currency are not read by the facts yet |
| PayWithoutAmount | text: "market related" | `JobPayInterval` | JobJack | `Opportunity_JobPayInterval` | pending from Yoma | today (CF first) | |
| Schedule | text | `JobWorkSchedule`, `JobEmploymentType` | JobJack: schedule. Jobberman: schedule, type | `Opportunity_JobWorkSchedule`, `Opportunity_JobEmploymentType` | pending from Yoma | today (CF first) | |
| Duration | — | `JobEmploymentDuration`, `JobEmploymentDurationUnit` | — | `Opportunity_JobEmploymentDuration`, `_JobEmploymentDurationUnit` | pending from Yoma | should | Not read; useful for "contract / temporary" in YouthFit |
| Location | `Countries`; text | — | — | `MyOpportunity_Location` | pending from Yoma | today | |
| Requirements | text | `JobMinimumQualification`, `JobExperienceLevel` | Jobberman | — (qualification); — (experience) | pending from Yoma | today (CF first) | Neither field is in the Job schema |
| ExperienceLevel (YouthFit) | — | `JobExperienceLevel` (None, EntryJunior, Mid, Senior) | Jobberman | — | pending from Yoma | today | |
| YearsRequired, MonthsRequired, EntryLevelWording, SchoolLevelOnly | text only | — | — | — | pending from Yoma | today | **should**: `JobExperienceLevel` and `JobMinimumQualification` when present (already first for Requirements) |
| Industry / category | — | `JobIndustry`, `JobCategory` | JobJack: industry | `Opportunity_JobIndustry`, `Opportunity_JobCategory` | pending from Yoma | should | Not read; could set the job's goal match on paths |
| Preferred skills | `Skills` | `JobPreferredSkills` | — | — | pending from Yoma | should | Paths match jobs by title pattern (`JobsPattern`) today |
| HowToApply | `URL`; text | — | — | — | pending from Yoma | today | |
| JobOpen, JobFresh | `DateEnd`, `DateStart`; feed sync (`SyncedInfo`, `PartnerSync.ProcessingLog`) | — | feed sync record | — | pending from Yoma | today | |
| NoScamSignals | text | — | — | — | pending from Yoma | today | |
| Job start (employment outcome) | `MyOpportunity` | `JobEmploymentStartDate` (MyOpportunity) | — | `MyOpportunity_JobEmploymentStartDate` | pending from Yoma | should | The Outcomes factor counts Job completions, not this date |

The public search does return `customFields`, but it was empty for every live task fetched on 27 Nov 2026 (41 of 41), and
the demo's job import does not copy them. The demo's job grades therefore come from listing text alone. Live Yoma reads
the custom fields.

## 4. Impact actions (W4)

| Ziqi field | Opportunity / record | Custom field | SSI attribute (now, ImpactAction schema) | v5 schema | Ziqi reads | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Type | `Type` = `ImpactAction` (live public name "Task", same id `f12a9d90-…`) | — | `Opportunity_Type` | pending from Yoma | today | Renamed in place by the CF configuration migration |
| Listing checks | as in section 1 | — | — | pending from Yoma | today | `VerifyTypes` includes ImpactAction in the demo |
| Difficulty | — | `ImpactActionDifficulty` (EntryLevel, ExperienceNeeded, SkillsRequired) | — | pending from Yoma | should | The pathway catalogue reads only `LearningDifficulty`. The demo import fills this field from the live difficulty |
| Tools needed (step card, "what you need") | — | `ImpactActionToolsRequired`, `ImpactActionToolsOtherDescription` | — | pending from Yoma | should | Not read; a natural "you will need a smartphone" line |
| Activity type | — | `ImpactActionVerifiedActivityType` | `Opportunity_ImpactActionVerifiedActivityType` | pending from Yoma | should | Not read |
| Impact achieved (outcome) | `MyOpportunity` | `ImpactActionImpactAchieved` (MyOpportunity) | `MyOpportunity_ImpactActionImpactAchieved` | pending from Yoma | should | Candidate input for an impact-step outcome once "good" is defined |
| Skills added | `Skills` | — | `Opportunity_Skills` | pending from Yoma | today | |

## 5. Pathway inputs and outcomes (My Yoma, W3, W4)

| Ziqi field | Opportunity / record | Custom field | SSI attribute (now) | v5 schema | Ziqi reads | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Goal (`goalKey`) | `UserPreferences.Goal` / `GoalId`; Ziqi's pathway profile | — | — | pending from Yoma | today (profile) / should (preferences, W3) | Answered from preferences when set (W3) |
| Hours per week | `UserPreferences.CommitmentInterval` + `CommitmentIntervalCount` | — | — | pending from Yoma | should (W3) | |
| Known skills | `UserPreferences.SkillsSelfAttested`; verified skills from YoID | — | `Opportunity_Skills` on earned credentials | pending from Yoma | should (W3) | |
| Background (school, study, degree) | `User.Education` | — | `User_Education` (YoID ACR) | pending from Yoma | should (W3) | |
| Country | `User.Country`; profile `countryId` | — | `User_Country` (YoID ACR) | pending from Yoma | today (profile) / should (W3) | |
| Engagement type, languages, categories | `UserPreferences.EngagementTypes`, `Languages`, `Categories` | — | — | pending from Yoma | should | Not used by paths yet |
| Stage match | `Title`, `Skills` against `Pathway:Goals[].Stages[].Pattern` | — | — | pending from Yoma | today | |
| Step type per stage (W4) | `Type` against `Stages[].Types` | — | `Opportunity_Type` | pending from Yoma | today (A3) | |
| Step status (Completed, InProgress) | `MyOpportunity` Verification + Completed; NavigatedExternalLink | — | the opportunity credential issued on completion | pending from Yoma | today | |
| Progression / Employment rate | later `MyOpportunity` completions (Learning; Job) | — | — | pending from Yoma | today | Hidden below 20 youth (`Pathway:MinimumGroupSize`) |
| Usually go on to (W4) | later completions per step (any step type) | — | — | pending from Yoma | today (A3) | Demo: synthetic, labelled (`Pathway__DemoDataNote`) |
| Drop-off (W5) | `PathwayStepEvent` | — | — | pending from Yoma | today (A4) | "Collecting from the pilot" until real data |
| Locked / swapped steps | `IPathwayStepStore` (A4) | — | — | pending from Yoma | today (A4) | |

## 6. Badges and the milestone credential

| Ziqi field | Opportunity / record | Custom field | SSI attribute (now) | v5 schema | Ziqi reads | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Partner badge (Bronze to Platinum) | organisation's Learning listings + Ziqi grades and assessments | — | `Opportunity_OrganizationName`, `Opportunity_OrganizationLogoURL` (issuer side) | pending from Yoma | n/a | Computed per request, never stored |
| Learner badges (Pathfinder … TopPickLearner) | pathway profile, `MyOpportunity` completions, grades | — | — | pending from Yoma | n/a | Computed per request |
| VerifiedSkill | a **verified** completion that awards a skill | — | `Opportunity_Skills`, `Opportunity_VerificationMethod`, `MyOpportunity_VerificationEvidence` | pending from Yoma | today | |
| Pathway milestone credential (W3, proposal) | the completed stage's steps | — | proposed on the current Opportunity schema: `Opportunity_Title` (stage name), `Opportunity_Summary` (goal), `Opportunity_Skills` (stage skills), `MyOpportunity_UserDisplayName`, `MyOpportunity_CommitmentIntervalDescription` (stage hours), `Opportunity_OrganizationName` (Yoma) | pending from Yoma | n/a | Not built; waits for v5 (iteration-4.md W6) |

## Requests to Yoma

1. Credential schemas v5: the attribute list per type, so the v5 column can be filled and the milestone credential built.
2. Fields Ziqi infers from page text today and would rather read from the listing: cost (free or paid, amount,
   currency), self-paced, format (course, webinar), certificate offered.
3. Whether `JobMinimumQualification` and `JobExperienceLevel` should join the Job schema. They decide YouthFit.
