// Prints SQL that adds every live Yoma Job from .cache/live-jobs.json to the demo database, for psql. Local demo only:
// start.sh calls it once, after the courses are in, when the database has no jobs yet.
// Jobs keep their live ids. A job pulled from a partner feed (JobJack, Jobberman) gets one processed Pull row in the
// partner sync log, which is how the API knows it is a feed job and skips the link check. Jobs carry no participation,
// and no custom fields: the public search does not return them, so pay and experience are read from the listing text.
// A job whose title or external id is already taken is skipped, as the live database's unique indexes would.
import { existsSync, readFileSync } from "node:fs";

const path = new URL("../.cache/live-jobs.json", import.meta.url);
if (!existsSync(path)) throw new Error("No live jobs. Run: node scripts/course-quality-demo/seed/fetch-live-catalogue.mjs --jobs");
const items = JSON.parse(readFileSync(path)).items;

const quote = (value) => (value === null || value === undefined ? "NULL" : `'${String(value).replaceAll("'", "''")}'`);
const now = `(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')`;
const orgAdmin = `(SELECT "Id" FROM "Entity"."User" WHERE "Email" = 'testorgadminuser@gmail.com')`;

const keep = ["id", "title", "description", "organizationId", "organizationName", "summary", "instructions", "url", "dateStart", "dateEnd",
  "featured", "engagementType", "keywords", "categories", "countries", "languages", "skills"];
const sql = [`\\set ON_ERROR_STOP on`, `BEGIN;`, `CREATE EXTENSION IF NOT EXISTS "pgcrypto";`, `CREATE TEMP TABLE live_job(d jsonb NOT NULL) ON COMMIT DROP;`];
for (let i = 0; i < items.length; i += 200) {
  const rows = items.slice(i, i + 200).map((item) => {
    const row = Object.fromEntries(keep.map((k) => [k, item[k] ?? null]));
    const feed = item.syncedInfo?.syncType === "Pull" ? item.syncedInfo.partners?.[0] : null;
    row.feedPartner = feed?.partner ?? null;
    row.externalId = feed?.externalId ?? null;
    return `(${quote(JSON.stringify(row))}::jsonb)`;
  });
  sql.push(`INSERT INTO live_job VALUES\n${rows.join(",\n")};`);
}

sql.push(`
-- Organisations not already imported with the courses.
INSERT INTO "Entity"."Organization"("Id","Name","NameHashValue","WebsiteURL","PrimaryContactName","PrimaryContactEmail","PrimaryContactPhone","VATIN","TaxNumber","RegistrationNumber","City","CountryId","StreetAddress","Province","PostalCode","Tagline","Biography","StatusId","CommentApproval","DateStatusModified","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT DISTINCT ON (d->>'organizationId') (d->>'organizationId')::uuid, d->>'organizationName', ENCODE(DIGEST(d->>'organizationName', 'sha256'), 'hex'), 'https://www.yoma.world/',
  'Demo Contact', 'demo@example.org', '+27125555555', 'GB123456789', '0123456789', '12345/28/14', 'Demo City', (SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"='ZA'),
  'Demo Street 1', 'Demo Province', '0000', 'Live Yoma organisation, imported for the Course Quality demo', 'Live Yoma organisation, imported for the Course Quality demo. Contact details are placeholders.',
  (SELECT "Id" FROM "Entity"."OrganizationStatus" WHERE "Name"='Active'), 'Approved', ${now}, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_job ORDER BY d->>'organizationId'
ON CONFLICT DO NOTHING;
INSERT INTO "Entity"."OrganizationProviderTypes"("Id","OrganizationId","ProviderTypeId","DateCreated")
  SELECT gen_random_uuid(), o.id, t."Id", ${now} FROM (SELECT DISTINCT (d->>'organizationId')::uuid AS id FROM live_job) o CROSS JOIN "Entity"."OrganizationProviderType" t
  WHERE EXISTS (SELECT 1 FROM "Entity"."Organization" x WHERE x."Id" = o.id)
ON CONFLICT DO NOTHING;

INSERT INTO "Opportunity"."Opportunity"("Id","Title","Description","TypeId","OrganizationId","Summary","Instructions","URL","ExternalId","RewardType","VerificationEnabled",
  "ParticipantCount","StatusId","Keywords","DateStart","DateEnd","CredentialIssuanceEnabled","Featured","EngagementTypeId","ShareWithPartners","Hidden",
  "DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT (d->>'id')::uuid, d->>'title', d->>'description', (SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"='Job'), (d->>'organizationId')::uuid,
  d->>'summary', d->>'instructions', NULLIF(btrim(d->>'url'), ''), d->>'externalId', 'None', FALSE,
  0, (SELECT "Id" FROM "Opportunity"."OpportunityStatus" WHERE "Name"='Active'),
  (SELECT string_agg(k, ',') FROM jsonb_array_elements_text(COALESCE(d->'keywords', '[]')) k), (d->>'dateStart')::timestamptz, (d->>'dateEnd')::timestamptz, FALSE,
  COALESCE((d->>'featured')::boolean, FALSE), et."Id", FALSE, FALSE, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_job
  JOIN "Entity"."Organization" org ON org."Id" = (d->>'organizationId')::uuid
  LEFT JOIN "Lookup"."EngagementType" et ON et."Name" = CASE d->>'engagementType' WHEN 'Online' THEN 'Remote' WHEN 'Offline' THEN 'OnSite' ELSE d->>'engagementType' END
ON CONFLICT DO NOTHING;

CREATE TEMP TABLE job_in ON COMMIT DROP AS
  SELECT l.d FROM live_job l JOIN "Opportunity"."Opportunity" o ON o."Id" = (l.d->>'id')::uuid AND o."TypeId" = (SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"='Job');

INSERT INTO "Opportunity"."OpportunityCategories"("Id","OpportunityId","CategoryId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM job_in, jsonb_array_elements(COALESCE(d->'categories', '[]')) c
  LEFT JOIN (VALUES ('d0d322ab-d1d7-44b6-94e8-7b85246aa42e'::uuid, '2ccbacf7-1ed9-4e20-bb7c-43edfdb3f950'::uuid),
    ('1dc39a5d-e049-4cfe-b708-855fce97b86e'::uuid, 'fa564c1c-591a-4a6d-8294-20165da8866b'::uuid)) merged(live_id, into_id) ON merged.live_id = (c->>'id')::uuid
  JOIN "Opportunity"."OpportunityCategory" k ON k."Id" = COALESCE(merged.into_id, (c->>'id')::uuid)) x;
INSERT INTO "Opportunity"."OpportunityCountries"("Id","OpportunityId","CountryId","DateCreated","DateModified")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now}, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM job_in, jsonb_array_elements(COALESCE(d->'countries', '[]')) c
  JOIN "Lookup"."Country" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunityLanguages"("Id","OpportunityId","LanguageId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.lid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS lid FROM job_in, jsonb_array_elements(COALESCE(d->'languages', '[]')) c
  JOIN "Lookup"."Language" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunitySkills"("Id","OpportunityId","SkillId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.sid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, s."Id" AS sid FROM job_in, jsonb_array_elements(COALESCE(d->'skills', '[]')) j
  JOIN "Lookup"."Skill" s ON s."Id" = (j->>'id')::uuid) x;

-- The feed's sync record: one processed Pull create per feed job, as the partner sync writes it.
INSERT INTO "PartnerSync"."ProcessingLog"("Id","EntityType","OpportunityId","PartnerId","Action","StatusId","EntityExternalId","SyncType","DateCreated","DateModified")
  SELECT gen_random_uuid(), 'Opportunity', (d->>'id')::uuid, p."Id", 'Create', (SELECT "Id" FROM "PartnerSync"."ProcessingStatus" WHERE "Name"='Processed'),
    d->>'externalId', 'Pull', ${now}, ${now}
  FROM job_in JOIN "PartnerSync"."Partner" p ON p."Name" = d->>'feedPartner';
COMMIT;`);

console.log(sql.join("\n"));
console.error(`${items.length} live jobs in the file, ${items.filter((o) => o.syncedInfo?.syncType === "Pull").length} from partner feeds.`);
