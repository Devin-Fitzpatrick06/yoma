// SQL for the live Job opportunities in .cache/live-jobs.json, so the My Yoma pathways can end in real open jobs.
// Jobs are listed only: no participation, completions or employment outcomes are invented for them.
// Usage: node seed/generate-jobs-sql.mjs | psql ...   (after generate-sql.mjs, which creates the demo users and lookups)
import { existsSync, readFileSync } from "node:fs";

const path = new URL("../.cache/live-jobs.json", import.meta.url);
if (!existsSync(path)) {
  console.error("No live jobs. Run: node scripts/course-quality-demo/seed/fetch-live-catalogue.mjs --jobs");
  process.exit(0);
}
const quote = (value) => (value === null || value === undefined ? "NULL" : `'${String(value).replaceAll("'", "''")}'`);
const now = `(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')`;
const orgAdmin = `(SELECT "Id" FROM "Entity"."User" WHERE "Email" = 'testorgadminuser@gmail.com')`;
const keep = ["id", "title", "description", "organizationId", "organizationName", "summary", "instructions", "url", "commitmentInterval",
  "commitmentIntervalCount", "keywords", "dateStart", "dateEnd", "engagementType", "categories", "countries", "languages", "skills"];

const { items } = JSON.parse(readFileSync(path));
const out = ["BEGIN;", `CREATE TEMP TABLE live_job(d jsonb NOT NULL) ON COMMIT DROP;`];
for (let i = 0; i < items.length; i += 200) {
  const rows = items.slice(i, i + 200).map((item) => `(${quote(JSON.stringify(Object.fromEntries(keep.map((k) => [k, item[k] ?? null]))))}::jsonb)`);
  out.push(`INSERT INTO live_job VALUES\n${rows.join(",\n")};`);
}
out.push(`
INSERT INTO "Entity"."Organization"("Id","Name","NameHashValue","WebsiteURL","PrimaryContactName","PrimaryContactEmail","PrimaryContactPhone","VATIN","TaxNumber","RegistrationNumber","City","CountryId","StreetAddress","Province","PostalCode","Tagline","Biography","StatusId","CommentApproval","DateStatusModified","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT DISTINCT ON (d->>'organizationId') (d->>'organizationId')::uuid, d->>'organizationName', ENCODE(DIGEST(d->>'organizationName', 'sha256'), 'hex'), 'https://www.yoma.world/',
  'Demo Contact', 'demo@example.org', '+27125555555', 'GB123456789', '0123456789', '12345/28/14', 'Demo City', (SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"='ZA'),
  'Demo Street 1', 'Demo Province', '0000', 'Live Yoma organisation, imported for the demo', 'Live Yoma organisation, imported for the demo. Contact details are placeholders.',
  (SELECT "Id" FROM "Entity"."OrganizationStatus" WHERE "Name"='Active'), 'Approved', ${now}, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_job ORDER BY d->>'organizationId'
ON CONFLICT DO NOTHING;
INSERT INTO "Entity"."OrganizationProviderTypes"("Id","OrganizationId","ProviderTypeId","DateCreated")
  SELECT gen_random_uuid(), o.id, t."Id", ${now} FROM (SELECT DISTINCT (d->>'organizationId')::uuid AS id FROM live_job) o CROSS JOIN "Entity"."OrganizationProviderType" t
  WHERE NOT EXISTS (SELECT 1 FROM "Entity"."OrganizationProviderTypes" x WHERE x."OrganizationId" = o.id AND x."ProviderTypeId" = t."Id");

INSERT INTO "Opportunity"."Opportunity"("Id","Title","Description","TypeId","OrganizationId","Summary","Instructions","URL","RewardType","VerificationEnabled",
  "CommitmentIntervalId","CommitmentIntervalCount","ParticipantCount","StatusId","Keywords","DateStart","DateEnd","CredentialIssuanceEnabled","Featured","EngagementTypeId","ShareWithPartners","Hidden",
  "DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT (d->>'id')::uuid, d->>'title', COALESCE(NULLIF(d->>'description', ''), d->>'title'), (SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"='Job'), (d->>'organizationId')::uuid,
  d->>'summary', d->>'instructions', NULLIF(btrim(d->>'url'), ''), 'None', FALSE,
  ti."Id", (d->>'commitmentIntervalCount')::smallint, 0, (SELECT "Id" FROM "Opportunity"."OpportunityStatus" WHERE "Name"='Active'),
  (SELECT string_agg(k, ',') FROM jsonb_array_elements_text(COALESCE(d->'keywords', '[]')) k), (d->>'dateStart')::timestamptz, (d->>'dateEnd')::timestamptz, FALSE,
  FALSE, et."Id", FALSE, FALSE, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_job
  LEFT JOIN "Lookup"."TimeInterval" ti ON ti."Name" = d->>'commitmentInterval'
  LEFT JOIN "Lookup"."EngagementType" et ON et."Name" = CASE d->>'engagementType' WHEN 'Online' THEN 'Remote' WHEN 'Offline' THEN 'OnSite' ELSE d->>'engagementType' END
ON CONFLICT DO NOTHING;

INSERT INTO "Opportunity"."OpportunityCategories"("Id","OpportunityId","CategoryId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM live_job, jsonb_array_elements(COALESCE(d->'categories', '[]')) c
  JOIN "Opportunity"."OpportunityCategory" k ON k."Id" = (c->>'id')::uuid) x;
INSERT INTO "Opportunity"."OpportunityCountries"("Id","OpportunityId","CountryId","DateCreated","DateModified")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now}, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM live_job, jsonb_array_elements(COALESCE(d->'countries', '[]')) c
  JOIN "Lookup"."Country" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunityLanguages"("Id","OpportunityId","LanguageId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.lid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS lid FROM live_job, jsonb_array_elements(COALESCE(d->'languages', '[]')) c
  JOIN "Lookup"."Language" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
COMMIT;`);
console.log(out.join("\n"));
console.error(`${items.length} live jobs`);
