// Prints SQL that adds every live Yoma Event from .cache/live-events.json to the demo database, for psql. Local demo only:
// start.sh calls it once, after the courses are in, when the database has no events yet (also after loading an older
// saved database that predates them). Modelled on generate-tasks-sql.mjs, without the synthetic pathway completions:
// events bring no participation, and the demo adds none.
//
// Type: live Yoma's public type list says "Event" (id e20dcceb-59c3-46d0-ab0a-d321d8bc4c31), the same name and id as
// this branch's seed (20240522063953_ApplicationDb_Featured_Types_Intervals_Seeding), so the import looks the type up by
// the local name 'Event'.
//
// Listings keep their live ids; categories by live id (with the two merged categories), countries and languages by
// code, skills by name (missing skills are added under their live ids). The live difficulty goes into the
// EventDifficulty custom field with the CF migration's mapping for events. An event pulled from a partner feed gets one
// processed Pull row in the partner sync log. Idempotent: an event whose id, title or external id is already taken is
// skipped (ON CONFLICT DO NOTHING, as the live database's unique indexes would), and only listings this run inserted
// get categories, skills and custom fields, so running it twice adds nothing.
import { existsSync, readFileSync } from "node:fs";

const path = new URL("../.cache/live-events.json", import.meta.url);
if (!existsSync(path)) throw new Error("No live events. Run: node scripts/course-quality-demo/seed/fetch-live-catalogue.mjs --events");
const items = JSON.parse(readFileSync(path)).items;

const quote = (value) => (value === null || value === undefined ? "NULL" : `'${String(value).replaceAll("'", "''")}'`);
const now = `(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')`;
const orgAdmin = `(SELECT "Id" FROM "Entity"."User" WHERE "Email" = 'testorgadminuser@gmail.com')`;
const eventType = `(SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"='Event')`;
const keep = ["id", "title", "description", "organizationId", "organizationName", "summary", "instructions", "url", "zltoReward", "zltoRewardCumulative",
  "verificationEnabled", "verificationMethod", "difficulty", "commitmentInterval", "commitmentIntervalCount", "participantLimit", "keywords", "dateStart", "dateEnd",
  "featured", "engagementType", "shareWithPartners", "categories", "countries", "languages", "skills", "verificationTypes"];
const sql = [`\\set ON_ERROR_STOP on`, `BEGIN;`, `CREATE EXTENSION IF NOT EXISTS "pgcrypto";`, `CREATE TEMP TABLE live_event(d jsonb NOT NULL) ON COMMIT DROP;`];
for (let i = 0; i < items.length; i += 200) {
  const rows = items.slice(i, i + 200).map((item) => {
    const row = Object.fromEntries(keep.map((k) => [k, item[k] ?? null]));
    const feed = item.syncedInfo?.syncType === "Pull" ? item.syncedInfo.partners?.[0] : null;
    row.feedPartner = feed?.partner ?? null;
    row.externalId = feed?.externalId ?? null;
    return `(${quote(JSON.stringify(row))}::jsonb)`;
  });
  sql.push(`INSERT INTO live_event VALUES\n${rows.join(",\n")};`);
}

sql.push(`
-- Organisations not already imported with the courses.
INSERT INTO "Entity"."Organization"("Id","Name","NameHashValue","WebsiteURL","PrimaryContactName","PrimaryContactEmail","PrimaryContactPhone","VATIN","TaxNumber","RegistrationNumber","City","CountryId","StreetAddress","Province","PostalCode","Tagline","Biography","StatusId","CommentApproval","DateStatusModified","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT DISTINCT ON (d->>'organizationId') (d->>'organizationId')::uuid, d->>'organizationName', ENCODE(DIGEST(d->>'organizationName', 'sha256'), 'hex'), 'https://www.yoma.world/',
  'Demo Contact', 'demo@example.org', '+27125555555', 'GB123456789', '0123456789', '12345/28/14', 'Demo City', (SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"='ZA'),
  'Demo Street 1', 'Demo Province', '0000', 'Live Yoma organisation, imported for the Course Quality demo', 'Live Yoma organisation, imported for the Course Quality demo. Contact details are placeholders.',
  (SELECT "Id" FROM "Entity"."OrganizationStatus" WHERE "Name"='Active'), 'Approved', ${now}, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_event ORDER BY d->>'organizationId'
ON CONFLICT DO NOTHING;
INSERT INTO "Entity"."OrganizationProviderTypes"("Id","OrganizationId","ProviderTypeId","DateCreated")
  SELECT gen_random_uuid(), o.id, t."Id", ${now} FROM (SELECT DISTINCT (d->>'organizationId')::uuid AS id FROM live_event) o CROSS JOIN "Entity"."OrganizationProviderType" t
  WHERE EXISTS (SELECT 1 FROM "Entity"."Organization" x WHERE x."Id" = o.id)
ON CONFLICT DO NOTHING;

CREATE TEMP TABLE event_skill_map ON COMMIT DROP AS SELECT DISTINCT ON (lower("Name")) lower("Name") AS k, "Id" AS id FROM "Lookup"."Skill" ORDER BY lower("Name"), "DateCreated";
CREATE UNIQUE INDEX ON event_skill_map(k);
INSERT INTO "Lookup"."Skill"("Id","Name","InfoURL","ExternalId","DateCreated","DateModified")
  SELECT DISTINCT ON (lower(s->>'name')) (s->>'id')::uuid, s->>'name', s->>'infoURL', COALESCE(substring(s->>'infoURL' from '[^/]+$'), s->>'id'), ${now}, ${now}
  FROM live_event, jsonb_array_elements(COALESCE(d->'skills', '[]')) s WHERE NOT EXISTS (SELECT 1 FROM event_skill_map m WHERE m.k = lower(s->>'name'))
  ORDER BY lower(s->>'name') ON CONFLICT DO NOTHING;
INSERT INTO event_skill_map SELECT DISTINCT ON (lower("Name")) lower("Name"), "Id" FROM "Lookup"."Skill" ORDER BY lower("Name"), "DateCreated" ON CONFLICT DO NOTHING;

INSERT INTO "Opportunity"."Opportunity"("Id","Title","Description","TypeId","OrganizationId","Summary","Instructions","URL","ExternalId","ZltoReward","ZltoRewardCumulative","RewardType",
  "VerificationEnabled","VerificationMethod","CommitmentIntervalId","CommitmentIntervalCount","ParticipantLimit","ParticipantCount","StatusId","Keywords","DateStart","DateEnd",
  "CredentialIssuanceEnabled","Featured","EngagementTypeId","ShareWithPartners","Hidden","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT (d->>'id')::uuid, d->>'title', d->>'description', ${eventType}, (d->>'organizationId')::uuid,
  d->>'summary', d->>'instructions', NULLIF(btrim(d->>'url'), ''), d->>'externalId', (d->>'zltoReward')::numeric, (d->>'zltoRewardCumulative')::numeric,
  CASE WHEN (d->>'zltoReward')::numeric > 0 THEN 'ZLTO' ELSE 'None' END, COALESCE((d->>'verificationEnabled')::boolean, FALSE), d->>'verificationMethod',
  ti."Id", (d->>'commitmentIntervalCount')::smallint, (d->>'participantLimit')::int, 0, (SELECT "Id" FROM "Opportunity"."OpportunityStatus" WHERE "Name"='Active'),
  (SELECT string_agg(k, ',') FROM jsonb_array_elements_text(COALESCE(d->'keywords', '[]')) k), (d->>'dateStart')::timestamptz, (d->>'dateEnd')::timestamptz, FALSE,
  COALESCE((d->>'featured')::boolean, FALSE), et."Id", COALESCE((d->>'shareWithPartners')::boolean, FALSE), FALSE, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_event
  JOIN "Entity"."Organization" org ON org."Id" = (d->>'organizationId')::uuid
  LEFT JOIN "Lookup"."TimeInterval" ti ON ti."Name" = d->>'commitmentInterval'
  -- live Yoma says Online and Offline, this database Remote and OnSite
  LEFT JOIN "Lookup"."EngagementType" et ON et."Name" = CASE d->>'engagementType' WHEN 'Online' THEN 'Remote' WHEN 'Offline' THEN 'OnSite' ELSE d->>'engagementType' END
ON CONFLICT DO NOTHING;

-- Only the events this run inserted (a listing skipped as a duplicate keeps what it had).
CREATE TEMP TABLE event_in ON COMMIT DROP AS
  SELECT l.d FROM live_event l JOIN "Opportunity"."Opportunity" o ON o."Id" = (l.d->>'id')::uuid AND o."TypeId" = ${eventType}
  WHERE NOT EXISTS (SELECT 1 FROM "Opportunity"."OpportunityCategories" c WHERE c."OpportunityId" = o."Id");

-- By id, as this branch's category migration does: it renamed eight live categories in place and merged two into others.
INSERT INTO "Opportunity"."OpportunityCategories"("Id","OpportunityId","CategoryId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM event_in, jsonb_array_elements(COALESCE(d->'categories', '[]')) c
  LEFT JOIN (VALUES ('d0d322ab-d1d7-44b6-94e8-7b85246aa42e'::uuid, '2ccbacf7-1ed9-4e20-bb7c-43edfdb3f950'::uuid),
    ('1dc39a5d-e049-4cfe-b708-855fce97b86e'::uuid, 'fa564c1c-591a-4a6d-8294-20165da8866b'::uuid)) merged(live_id, into_id) ON merged.live_id = (c->>'id')::uuid
  JOIN "Opportunity"."OpportunityCategory" k ON k."Id" = COALESCE(merged.into_id, (c->>'id')::uuid)) x;
INSERT INTO "Opportunity"."OpportunityCountries"("Id","OpportunityId","CountryId","DateCreated","DateModified")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now}, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM event_in, jsonb_array_elements(COALESCE(d->'countries', '[]')) c
  JOIN "Lookup"."Country" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunityLanguages"("Id","OpportunityId","LanguageId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.lid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS lid FROM event_in, jsonb_array_elements(COALESCE(d->'languages', '[]')) c
  JOIN "Lookup"."Language" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunitySkills"("Id","OpportunityId","SkillId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.sid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, m.id AS sid FROM event_in, jsonb_array_elements(COALESCE(d->'skills', '[]')) s
  JOIN event_skill_map m ON m.k = lower(s->>'name')) x;
INSERT INTO "Opportunity"."OpportunityVerificationTypes"("Id","OpportunityId","VerificationTypeId","Description","DateCreated","DateModified")
  SELECT gen_random_uuid(), (d->>'id')::uuid, k."Id", left(v->>'description', 255), ${now}, ${now} FROM event_in, jsonb_array_elements(COALESCE(d->'verificationTypes', '[]')) v
  JOIN "Opportunity"."OpportunityVerificationType" k ON k."Name" = v->>'type';

-- Live difficulty into the EventDifficulty custom field, with the CF migration's mapping for events.
INSERT INTO "Core"."CustomFieldValue"("Id","CustomFieldDefinitionId","OpportunityId","Value","DateCreated","DateModified")
  SELECT gen_random_uuid(), def."Id", (d->>'id')::uuid,
    CASE d->>'difficulty' WHEN 'Any Level' THEN 'OpenToAll' WHEN 'Beginner' THEN 'OpenToAll' WHEN 'Intermediate' THEN 'FamiliarityNeeded' WHEN 'Advanced' THEN 'ExperiencedIndividuals' END,
    ${now}, ${now}
  FROM event_in JOIN "Core"."CustomFieldDefinition" def ON def."EntityType" = 'Opportunity' AND def."Key" = 'EventDifficulty'
  WHERE d->>'difficulty' IN ('Any Level', 'Beginner', 'Intermediate', 'Advanced');

-- The feed's sync record: one processed Pull create per feed event (none on live Yoma today), as the partner sync writes it.
INSERT INTO "PartnerSync"."ProcessingLog"("Id","EntityType","OpportunityId","PartnerId","Action","StatusId","EntityExternalId","SyncType","DateCreated","DateModified")
  SELECT gen_random_uuid(), 'Opportunity', (d->>'id')::uuid, p."Id", 'Create', (SELECT "Id" FROM "PartnerSync"."ProcessingStatus" WHERE "Name"='Processed'),
    d->>'externalId', 'Pull', ${now}, ${now}
  FROM event_in JOIN "PartnerSync"."Partner" p ON p."Name" = d->>'feedPartner';`);

sql.push(`COMMIT;`);

console.log(sql.join("\n"));
console.error(`${items.length} live events in the file, ${items.filter((o) => o.syncedInfo?.syncType === "Pull").length} from partner feeds, ${items.filter((o) => !o.url?.trim()).length} with no link.`);
