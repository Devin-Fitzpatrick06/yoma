// Prints SQL that adds every live Yoma Task from .cache/live-tasks.json to the demo database as an impact action, then
// a small, labelled set of synthetic pathway completions on them, for psql. Local demo only: start.sh calls it once,
// after the courses are in and before the jobs, when the database has no impact actions yet.
//
// Type: live Yoma's public type list says "Task" (id f12a9d90-a8f6-4914-8ca5-6acf209f7312). This branch's custom-field
// migration (20260922063352_ApplicationDb_CF_Configuration) renames that row in place to Name "ImpactAction", DisplayName
// "Impact Action", keeping the id, so the import looks the type up by the local name 'ImpactAction'.
//
// Listings keep their live ids, as courses and jobs do; categories by live id (with the two merged categories), countries
// and languages by code, skills by name (missing skills are added under their live ids, as for courses). The live
// difficulty goes into the ImpactActionDifficulty custom field with the migration's mapping. A task pulled from a partner
// feed (IXO) gets one processed Pull row in the partner sync log, as jobs do. A task whose title or external id is
// already taken is skipped, as the live database's unique indexes would.
//
// Participation: the live listings bring none. Instead, per demo goal, one chain "course A -> impact action B -> course C"
// is completed by some of the demo learners who already completed A (see pathwayChainsSql below). Completions only:
// no starts without a completion are added, so no drop-off is implied. Every row says "Demo data" in CommentVerification
// and comes from the 50 synthetic demo learners, whose figures My Yoma labels with Pathway__DemoDataNote.
import { existsSync, readFileSync } from "node:fs";

const path = new URL("../.cache/live-tasks.json", import.meta.url);
if (!existsSync(path)) throw new Error("No live tasks. Run: node scripts/course-quality-demo/seed/fetch-live-catalogue.mjs --tasks");
const items = JSON.parse(readFileSync(path)).items;

// The goals and stages are the API's own (Pathway:Goals). In the runner the file is mounted at /appsettings.json.
const settingsPaths = ["/appsettings.json", new URL("../../../src/api/src/application/Yoma.Core.Api/appsettings.json", import.meta.url)];
const settingsPath = settingsPaths.find((p) => existsSync(p));
if (!settingsPath) throw new Error("appsettings.json not found, so the demo goals are unknown.");
const goals = JSON.parse(readFileSync(settingsPath)).Pathway?.Goals ?? [];

const quote = (value) => (value === null || value === undefined ? "NULL" : `'${String(value).replaceAll("'", "''")}'`);
const now = `(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')`;
const orgAdmin = `(SELECT "Id" FROM "Entity"."User" WHERE "Email" = 'testorgadminuser@gmail.com')`;
const impactType = `(SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"='ImpactAction')`;
// .NET regex to PostgreSQL: a word boundary is \y there (\b is a backspace)
const pgPattern = (pattern) => pattern.replaceAll("\\b", "\\y");

// The chain sizes. A course needs this many demo completers, more than 45 days ago, to start a chain; this many of them
// complete the impact action (at least the API's minimum group size of 20, so its "usually go on to" can show); and up
// to this many of those complete the next course.
const chain = { minCompleters: 21, impactCompleters: 24, nextCompleters: 16 };

const keep = ["id", "title", "description", "organizationId", "organizationName", "summary", "instructions", "url", "zltoReward", "zltoRewardCumulative",
  "verificationEnabled", "verificationMethod", "difficulty", "commitmentInterval", "commitmentIntervalCount", "participantLimit", "keywords", "dateStart", "dateEnd",
  "featured", "engagementType", "shareWithPartners", "categories", "countries", "languages", "skills", "verificationTypes"];
const sql = [`\\set ON_ERROR_STOP on`, `BEGIN;`, `CREATE EXTENSION IF NOT EXISTS "pgcrypto";`, `CREATE TEMP TABLE live_task(d jsonb NOT NULL) ON COMMIT DROP;`];
for (let i = 0; i < items.length; i += 200) {
  const rows = items.slice(i, i + 200).map((item) => {
    const row = Object.fromEntries(keep.map((k) => [k, item[k] ?? null]));
    const feed = item.syncedInfo?.syncType === "Pull" ? item.syncedInfo.partners?.[0] : null;
    row.feedPartner = feed?.partner ?? null;
    row.externalId = feed?.externalId ?? null;
    return `(${quote(JSON.stringify(row))}::jsonb)`;
  });
  sql.push(`INSERT INTO live_task VALUES\n${rows.join(",\n")};`);
}

sql.push(`
-- Organisations not already imported with the courses.
INSERT INTO "Entity"."Organization"("Id","Name","NameHashValue","WebsiteURL","PrimaryContactName","PrimaryContactEmail","PrimaryContactPhone","VATIN","TaxNumber","RegistrationNumber","City","CountryId","StreetAddress","Province","PostalCode","Tagline","Biography","StatusId","CommentApproval","DateStatusModified","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT DISTINCT ON (d->>'organizationId') (d->>'organizationId')::uuid, d->>'organizationName', ENCODE(DIGEST(d->>'organizationName', 'sha256'), 'hex'), 'https://www.yoma.world/',
  'Demo Contact', 'demo@example.org', '+27125555555', 'GB123456789', '0123456789', '12345/28/14', 'Demo City', (SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"='ZA'),
  'Demo Street 1', 'Demo Province', '0000', 'Live Yoma organisation, imported for the Course Quality demo', 'Live Yoma organisation, imported for the Course Quality demo. Contact details are placeholders.',
  (SELECT "Id" FROM "Entity"."OrganizationStatus" WHERE "Name"='Active'), 'Approved', ${now}, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_task ORDER BY d->>'organizationId'
ON CONFLICT DO NOTHING;
INSERT INTO "Entity"."OrganizationProviderTypes"("Id","OrganizationId","ProviderTypeId","DateCreated")
  SELECT gen_random_uuid(), o.id, t."Id", ${now} FROM (SELECT DISTINCT (d->>'organizationId')::uuid AS id FROM live_task) o CROSS JOIN "Entity"."OrganizationProviderType" t
  WHERE EXISTS (SELECT 1 FROM "Entity"."Organization" x WHERE x."Id" = o.id)
ON CONFLICT DO NOTHING;

CREATE TEMP TABLE task_skill_map ON COMMIT DROP AS SELECT DISTINCT ON (lower("Name")) lower("Name") AS k, "Id" AS id FROM "Lookup"."Skill" ORDER BY lower("Name"), "DateCreated";
CREATE UNIQUE INDEX ON task_skill_map(k);
INSERT INTO "Lookup"."Skill"("Id","Name","InfoURL","ExternalId","DateCreated","DateModified")
  SELECT DISTINCT ON (lower(s->>'name')) (s->>'id')::uuid, s->>'name', s->>'infoURL', COALESCE(substring(s->>'infoURL' from '[^/]+$'), s->>'id'), ${now}, ${now}
  FROM live_task, jsonb_array_elements(COALESCE(d->'skills', '[]')) s WHERE NOT EXISTS (SELECT 1 FROM task_skill_map m WHERE m.k = lower(s->>'name'))
  ORDER BY lower(s->>'name') ON CONFLICT DO NOTHING;
INSERT INTO task_skill_map SELECT DISTINCT ON (lower("Name")) lower("Name"), "Id" FROM "Lookup"."Skill" ORDER BY lower("Name"), "DateCreated" ON CONFLICT DO NOTHING;

INSERT INTO "Opportunity"."Opportunity"("Id","Title","Description","TypeId","OrganizationId","Summary","Instructions","URL","ExternalId","ZltoReward","ZltoRewardCumulative","RewardType",
  "VerificationEnabled","VerificationMethod","CommitmentIntervalId","CommitmentIntervalCount","ParticipantLimit","ParticipantCount","StatusId","Keywords","DateStart","DateEnd",
  "CredentialIssuanceEnabled","Featured","EngagementTypeId","ShareWithPartners","Hidden","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT (d->>'id')::uuid, d->>'title', d->>'description', ${impactType}, (d->>'organizationId')::uuid,
  d->>'summary', d->>'instructions', NULLIF(btrim(d->>'url'), ''), d->>'externalId', (d->>'zltoReward')::numeric, (d->>'zltoRewardCumulative')::numeric,
  CASE WHEN (d->>'zltoReward')::numeric > 0 THEN 'ZLTO' ELSE 'None' END, COALESCE((d->>'verificationEnabled')::boolean, FALSE), d->>'verificationMethod',
  ti."Id", (d->>'commitmentIntervalCount')::smallint, (d->>'participantLimit')::int, 0, (SELECT "Id" FROM "Opportunity"."OpportunityStatus" WHERE "Name"='Active'),
  (SELECT string_agg(k, ',') FROM jsonb_array_elements_text(COALESCE(d->'keywords', '[]')) k), (d->>'dateStart')::timestamptz, (d->>'dateEnd')::timestamptz, FALSE,
  COALESCE((d->>'featured')::boolean, FALSE), et."Id", COALESCE((d->>'shareWithPartners')::boolean, FALSE), FALSE, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_task
  JOIN "Entity"."Organization" org ON org."Id" = (d->>'organizationId')::uuid
  LEFT JOIN "Lookup"."TimeInterval" ti ON ti."Name" = d->>'commitmentInterval'
  -- live Yoma says Online and Offline, this database Remote and OnSite
  LEFT JOIN "Lookup"."EngagementType" et ON et."Name" = CASE d->>'engagementType' WHEN 'Online' THEN 'Remote' WHEN 'Offline' THEN 'OnSite' ELSE d->>'engagementType' END
ON CONFLICT DO NOTHING;

-- Only the tasks this run inserted (a listing skipped as a duplicate keeps what it had).
CREATE TEMP TABLE task_in ON COMMIT DROP AS
  SELECT l.d FROM live_task l JOIN "Opportunity"."Opportunity" o ON o."Id" = (l.d->>'id')::uuid AND o."TypeId" = ${impactType}
  WHERE NOT EXISTS (SELECT 1 FROM "Opportunity"."OpportunityCategories" c WHERE c."OpportunityId" = o."Id");

-- By id, as this branch's category migration does: it renamed eight live categories in place and merged two into others.
INSERT INTO "Opportunity"."OpportunityCategories"("Id","OpportunityId","CategoryId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM task_in, jsonb_array_elements(COALESCE(d->'categories', '[]')) c
  LEFT JOIN (VALUES ('d0d322ab-d1d7-44b6-94e8-7b85246aa42e'::uuid, '2ccbacf7-1ed9-4e20-bb7c-43edfdb3f950'::uuid),
    ('1dc39a5d-e049-4cfe-b708-855fce97b86e'::uuid, 'fa564c1c-591a-4a6d-8294-20165da8866b'::uuid)) merged(live_id, into_id) ON merged.live_id = (c->>'id')::uuid
  JOIN "Opportunity"."OpportunityCategory" k ON k."Id" = COALESCE(merged.into_id, (c->>'id')::uuid)) x;
INSERT INTO "Opportunity"."OpportunityCountries"("Id","OpportunityId","CountryId","DateCreated","DateModified")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now}, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM task_in, jsonb_array_elements(COALESCE(d->'countries', '[]')) c
  JOIN "Lookup"."Country" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunityLanguages"("Id","OpportunityId","LanguageId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.lid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS lid FROM task_in, jsonb_array_elements(COALESCE(d->'languages', '[]')) c
  JOIN "Lookup"."Language" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunitySkills"("Id","OpportunityId","SkillId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.sid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, m.id AS sid FROM task_in, jsonb_array_elements(COALESCE(d->'skills', '[]')) s
  JOIN task_skill_map m ON m.k = lower(s->>'name')) x;
INSERT INTO "Opportunity"."OpportunityVerificationTypes"("Id","OpportunityId","VerificationTypeId","Description","DateCreated","DateModified")
  SELECT gen_random_uuid(), (d->>'id')::uuid, k."Id", left(v->>'description', 255), ${now}, ${now} FROM task_in, jsonb_array_elements(COALESCE(d->'verificationTypes', '[]')) v
  JOIN "Opportunity"."OpportunityVerificationType" k ON k."Name" = v->>'type';

-- Live difficulty into the ImpactActionDifficulty custom field, with the CF migration's mapping for impact actions.
INSERT INTO "Core"."CustomFieldValue"("Id","CustomFieldDefinitionId","OpportunityId","Value","DateCreated","DateModified")
  SELECT gen_random_uuid(), def."Id", (d->>'id')::uuid,
    CASE d->>'difficulty' WHEN 'Any Level' THEN 'EntryLevel' WHEN 'Beginner' THEN 'EntryLevel' WHEN 'Intermediate' THEN 'ExperienceNeeded' WHEN 'Advanced' THEN 'SkillsRequired' END,
    ${now}, ${now}
  FROM task_in JOIN "Core"."CustomFieldDefinition" def ON def."EntityType" = 'Opportunity' AND def."Key" = 'ImpactActionDifficulty'
  WHERE d->>'difficulty' IN ('Any Level', 'Beginner', 'Intermediate', 'Advanced');

-- The feed's sync record: one processed Pull create per feed task, as the partner sync writes it.
INSERT INTO "PartnerSync"."ProcessingLog"("Id","EntityType","OpportunityId","PartnerId","Action","StatusId","EntityExternalId","SyncType","DateCreated","DateModified")
  SELECT gen_random_uuid(), 'Opportunity', (d->>'id')::uuid, p."Id", 'Create', (SELECT "Id" FROM "PartnerSync"."ProcessingStatus" WHERE "Name"='Processed'),
    d->>'externalId', 'Pull', ${now}, ${now}
  FROM task_in JOIN "PartnerSync"."Partner" p ON p."Name" = d->>'feedPartner';`);

sql.push(...pathwayChainsSql());
sql.push(`COMMIT;`);

console.log(sql.join("\n"));
console.error(`${items.length} live tasks in the file, ${items.filter((o) => o.syncedInfo?.syncType === "Pull").length} from partner feeds; ${goals.length} goals for the demo chains.`);

// ---- Synthetic pathway chains ----
// Per goal, in the order of Pathway:Goals:
//   A = the course of any stage but the goal's last with the most demo completers more than 45 days ago (at least
//       chain.minCompleters), earlier stages first on a tie, so the chain starts from a step a path is likely to show;
//   B = the impact action most plausibly next: it matches one of the goal's stage patterns (title or a skill), shares
//       skills with A, or shares A's category. One not used by an earlier goal is preferred, so more impact steps can
//       show "usually go on to". A goal with no plausible impact action gets no chain;
//   C = a course of the next stage of the goal after A that has one (later stages after that) which enough of the
//       chain's learners have not completed yet, preferring one that shares skills or a category with A or B.
// chain.impactCompleters of A's completers (picked by a hash of their email, so every run picks the same) complete B
// 6 to 12 days before today (and at least 3 days after A), and up to chain.nextCompleters of those complete C 2 to 5
// days after B. B and C are that recent so that few other completions fall after them: the "usually go on to" of B is
// then C, not whichever popular course a learner happened to finish later. Every date is in the past.
function pathwayChainsSql() {
  const stages = goals.flatMap((goal, gi) =>
    (goal.Stages ?? []).map((stage, si) => `(${quote(goal.Key)}, ${gi}, ${si}, ${quote(pgPattern(stage.Pattern ?? ""))})`));
  if (stages.length === 0) return [`-- No Pathway:Goals in appsettings.json, so no synthetic pathway chains.`];
  const learning = `(SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"='Learning')`;
  const verification = `(SELECT "Id" FROM "Opportunity"."MyOpportunityAction" WHERE "Name"='Verification')`;
  const completedStatus = `(SELECT "Id" FROM "Opportunity"."MyOpportunityVerificationStatus" WHERE "Name"='Completed')`;
  const note = "Demo data: a synthetic pathway completion by a demo learner, not a real youth.";
  return [`
-- Synthetic, labelled demo participation for "usually go on to" (W4). Completions only, never starts without one.
CREATE TEMP TABLE goal_stage(goal text, goal_order int, stage int, pattern text) ON COMMIT DROP;
INSERT INTO goal_stage VALUES
${stages.join(",\n")};
CREATE TEMP TABLE chain_opp ON COMMIT DROP AS
  SELECT o."Id" AS opp, o."Title" AS title, o."TypeId" = ${learning} AS is_course FROM "Opportunity"."Opportunity" o
  WHERE o."TypeId" IN (${learning}, ${impactType}) AND o."StatusId" = (SELECT "Id" FROM "Opportunity"."OpportunityStatus" WHERE "Name"='Active') AND o."Hidden" IS NOT TRUE;
CREATE TEMP TABLE chain_stage ON COMMIT DROP AS
  SELECT g.goal, g.goal_order, g.stage, o.opp, o.is_course FROM goal_stage g JOIN chain_opp o ON o.title ~* g.pattern
    OR EXISTS (SELECT 1 FROM "Opportunity"."OpportunitySkills" os JOIN "Lookup"."Skill" s ON s."Id" = os."SkillId" WHERE os."OpportunityId" = o.opp AND s."Name" ~* g.pattern);
CREATE TEMP TABLE demo_done ON COMMIT DROP AS
  SELECT mo."UserId" AS uid, u."Email" AS email, mo."OpportunityId" AS opp, mo."DateCompleted" AS at
  FROM "Opportunity"."MyOpportunity" mo JOIN "Entity"."User" u ON u."Id" = mo."UserId" AND u."Email" LIKE 'demo.learner%@example.org'
  WHERE mo."ActionId" = ${verification} AND mo."VerificationStatusId" = ${completedStatus} AND mo."DateCompleted" IS NOT NULL;
CREATE TEMP TABLE pathway_chain(goal text, goal_order int, a uuid, b uuid, c uuid) ON COMMIT DROP;
CREATE TEMP TABLE chain_learner(goal text, uid uuid, email text, a_at timestamptz, b_at timestamptz, c_ok boolean) ON COMMIT DROP;

DO $chains$
DECLARE
  g record; a record; b uuid; c uuid; used uuid[] := '{}';
BEGIN
  -- once only: a database that already has the synthetic pathway completions keeps them as they are
  IF EXISTS (SELECT 1 FROM "Opportunity"."MyOpportunity" WHERE "CommentVerification" = ${quote(note)}) THEN
    RAISE NOTICE 'the synthetic pathway completions are already in, none added';
    RETURN;
  END IF;
  FOR g IN SELECT goal, min(goal_order) AS goal_order FROM goal_stage GROUP BY goal ORDER BY 2 LOOP
    SELECT cs.opp, min(cs.stage) AS stage, (SELECT count(*) FROM demo_done d WHERE d.opp = cs.opp AND d.at < ${now} - INTERVAL '45 days') AS n INTO a
      FROM chain_stage cs WHERE cs.goal = g.goal AND cs.is_course
        AND cs.stage < (SELECT max(x.stage) FROM goal_stage x WHERE x.goal = g.goal) GROUP BY cs.opp
      ORDER BY 3 DESC, 2, cs.opp LIMIT 1;
    IF NOT FOUND OR a.n < ${chain.minCompleters} THEN RAISE NOTICE 'goal %: no course with % demo completers, no chain', g.goal, ${chain.minCompleters}; CONTINUE; END IF;

    SELECT t.opp INTO b FROM (
      SELECT o.opp,
        3 * (EXISTS (SELECT 1 FROM chain_stage cs WHERE cs.goal = g.goal AND cs.opp = o.opp))::int
        + 2 * (SELECT count(*) FROM "Opportunity"."OpportunitySkills" x JOIN "Opportunity"."OpportunitySkills" y ON y."SkillId" = x."SkillId" AND y."OpportunityId" = a.opp WHERE x."OpportunityId" = o.opp)::int
        + (EXISTS (SELECT 1 FROM "Opportunity"."OpportunityCategories" x JOIN "Opportunity"."OpportunityCategories" y ON y."CategoryId" = x."CategoryId" AND y."OpportunityId" = a.opp WHERE x."OpportunityId" = o.opp))::int AS score
      FROM chain_opp o WHERE NOT o.is_course) t
    WHERE t.score > 0 ORDER BY (t.opp = ANY(used)), t.score DESC, t.opp LIMIT 1;
    IF b IS NULL THEN RAISE NOTICE 'goal %: no impact action plausibly linked to its first course, no chain', g.goal; CONTINUE; END IF;
    used := used || b;

    -- the learners: A's completers more than 45 days ago, in a fixed order
    INSERT INTO chain_learner(goal, uid, email, a_at, b_at)
      SELECT g.goal, d.uid, d.email, d.at, GREATEST(d.at + make_interval(days => 3 + abs(hashtext(d.email || g.goal || 'b')) % 7),
        ${now} - make_interval(days => 12 - abs(hashtext(d.email || g.goal || 'b')) % 7))
      FROM demo_done d WHERE d.opp = a.opp AND d.at < ${now} - INTERVAL '45 days'
        AND NOT EXISTS (SELECT 1 FROM demo_done x WHERE x.uid = d.uid AND x.opp = b)
      ORDER BY md5(d.email || g.goal) LIMIT ${chain.impactCompleters};

    SELECT cs.opp INTO c FROM chain_stage cs
      WHERE cs.goal = g.goal AND cs.is_course AND cs.stage > a.stage AND cs.opp <> a.opp
      GROUP BY cs.opp
      HAVING (SELECT count(*) FROM chain_learner l WHERE l.goal = g.goal AND NOT EXISTS (SELECT 1 FROM demo_done x WHERE x.uid = l.uid AND x.opp = cs.opp)) >= ${Math.ceil(chain.nextCompleters * 0.75)}
      ORDER BY min(cs.stage),
        (SELECT count(DISTINCT x."SkillId") FROM "Opportunity"."OpportunitySkills" x JOIN "Opportunity"."OpportunitySkills" y ON y."SkillId" = x."SkillId" AND y."OpportunityId" IN (a.opp, b)
          WHERE x."OpportunityId" = cs.opp)
        + (SELECT count(DISTINCT x."CategoryId") FROM "Opportunity"."OpportunityCategories" x JOIN "Opportunity"."OpportunityCategories" y ON y."CategoryId" = x."CategoryId" AND y."OpportunityId" IN (a.opp, b)
          WHERE x."OpportunityId" = cs.opp) DESC,
        (SELECT count(*) FROM demo_done d WHERE d.opp = cs.opp) DESC, cs.opp LIMIT 1;

    UPDATE chain_learner l SET c_ok = TRUE WHERE l.goal = g.goal AND l.uid IN (
      SELECT x.uid FROM chain_learner x WHERE x.goal = g.goal AND c IS NOT NULL AND NOT EXISTS (SELECT 1 FROM demo_done y WHERE y.uid = x.uid AND y.opp = c)
      ORDER BY md5(x.email || g.goal || 'c') LIMIT ${chain.nextCompleters});
    INSERT INTO pathway_chain VALUES (g.goal, g.goal_order, a.opp, b, c);
    RAISE NOTICE 'goal %: course % -> impact action % -> course %', g.goal, a.opp, b, c;
  END LOOP;
END
$chains$;

INSERT INTO "Opportunity"."MyOpportunity"("Id","UserId","OpportunityId","ActionId","VerificationStatusId","CommentVerification","DateStart","DateEnd","DateCompleted","DateCreated","DateModified")
  SELECT gen_random_uuid(), l.uid, p.b, ${verification}, ${completedStatus}, ${quote(note)}, l.b_at - INTERVAL '1 day', l.b_at, l.b_at, l.b_at, l.b_at
  FROM chain_learner l JOIN pathway_chain p ON p.goal = l.goal
ON CONFLICT DO NOTHING;
INSERT INTO "Opportunity"."MyOpportunity"("Id","UserId","OpportunityId","ActionId","VerificationStatusId","CommentVerification","DateStart","DateEnd","DateCompleted","DateCreated","DateModified")
  SELECT gen_random_uuid(), l.uid, p.c, ${verification}, ${completedStatus}, ${quote(note)}, x.at - INTERVAL '2 days', x.at, x.at, x.at, x.at
  FROM chain_learner l JOIN pathway_chain p ON p.goal = l.goal AND p.c IS NOT NULL
    CROSS JOIN LATERAL (SELECT l.b_at + make_interval(days => 2 + abs(hashtext(l.email || l.goal || 'c')) % 4) AS at) x
  WHERE l.c_ok
ON CONFLICT DO NOTHING;

-- Search reads a listing's completed count from this column.
UPDATE "Opportunity"."Opportunity" o SET "ParticipantCount" = n.n
  FROM (SELECT mo."OpportunityId" AS id, count(*) AS n FROM "Opportunity"."MyOpportunity" mo
    WHERE mo."ActionId" = ${verification} AND mo."VerificationStatusId" = ${completedStatus}
      AND mo."OpportunityId" IN (SELECT b FROM pathway_chain UNION SELECT c FROM pathway_chain WHERE c IS NOT NULL) GROUP BY mo."OpportunityId") n
  WHERE o."Id" = n.id;

SELECT p.goal, a."Title" AS course, (SELECT count(*) FROM chain_learner l WHERE l.goal = p.goal) AS then_impact, b."Title" AS impact_action,
  (SELECT count(*) FROM chain_learner l WHERE l.goal = p.goal AND l.c_ok) AS then_course, c."Title" AS next_course
FROM pathway_chain p JOIN "Opportunity"."Opportunity" a ON a."Id" = p.a JOIN "Opportunity"."Opportunity" b ON b."Id" = p.b
  LEFT JOIN "Opportunity"."Opportunity" c ON c."Id" = p.c ORDER BY p.goal_order;`];
}
