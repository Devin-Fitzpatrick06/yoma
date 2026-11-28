// Prints SQL that adds the connected journey's listings to the demo database, for psql: the 5 organisations and 9
// opportunities of the shared mock data v1.1 (fictional, ../journey/shared-mock-data.json) under the journey contract's
// Yoma ids, and Halima's Yoma account. Local demo only: start.sh --journey calls it after the catalogue is in.
//
// Each listing links to its fictional page on the demo fixtures server (/journey/<shared id>), so Ziqi's checks read a
// page that agrees with the listing; opp-course-005's page returns 404 on purpose. Every organisation's tagline and every
// listing's summary and description say that they are sample data.
//
// Halima gets a Yoma account row like the other demo users (country, settings answered, YoID onboarded), which her local
// Keycloak sign-in (journey-user.sh) links to by email on her first sign-in. Like them, she has no saved preferences and
// no My Yoma profile: she answers My Yoma's questions in the demo. Her save of opp-job-001 is removed on every run, so the
// demo always starts with the job unsaved.
//
// Idempotent: organisations and listings are upserted by id, and each listing's categories, countries, languages and
// skills are replaced, so running it twice changes nothing.
import { dayOffset, isoDay } from "./dates.mjs";
import { halima, listings, opportunityIds, organizations } from "./journey.mjs";

const base = process.env.DEMO_BASE_DATE;
if (!base) throw new Error("DEMO_BASE_DATE is not set");

const quote = (value) => (value === null || value === undefined ? "NULL" : `'${String(value).replaceAll("'", "''")}'`);
const now = `(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')`;
const orgAdmin = `(SELECT "Id" FROM "Entity"."User" WHERE "Email" = 'testorgadminuser@gmail.com')`;
const nigeria = `(SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"='NG')`;
const halimaUser = `(SELECT "Id" FROM "Entity"."User" WHERE "Email" = ${quote(halima.email)})`;
const ids = (list) => list.map((o) => quote(o.id)).join(", ");

const out = [`\\set ON_ERROR_STOP on`, `BEGIN;`, `CREATE EXTENSION IF NOT EXISTS "pgcrypto";`];

for (const org of organizations) {
  out.push(`INSERT INTO "Entity"."Organization"("Id","Name","NameHashValue","WebsiteURL","PrimaryContactName","PrimaryContactEmail","PrimaryContactPhone","VATIN","TaxNumber","RegistrationNumber","City","CountryId","StreetAddress","Province","PostalCode","Tagline","Biography","StatusId","CommentApproval","DateStatusModified","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
VALUES (${quote(org.id)}, ${quote(org.name)}, ENCODE(DIGEST(${quote(org.name)}, 'sha256'), 'hex'), 'https://example.org/', 'Sample Contact', 'sample@example.org', '+2340000000000', 'NG000000000', '0000000000', 'RC000000', 'Kaduna', ${nigeria},
  'Sample Street 1', 'Kaduna State', '800001', ${quote(org.tagline)}, ${quote(org.biography)},
  (SELECT "Id" FROM "Entity"."OrganizationStatus" WHERE "Name"='Active'), 'Approved', ${now}, ${now}, ${orgAdmin}, ${now}, ${orgAdmin})
ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name", "NameHashValue" = EXCLUDED."NameHashValue", "Tagline" = EXCLUDED."Tagline", "Biography" = EXCLUDED."Biography",
  "StatusId" = EXCLUDED."StatusId", "DateModified" = ${now};
INSERT INTO "Entity"."OrganizationUsers"("Id","OrganizationId","UserId","DateCreated")
  SELECT gen_random_uuid(), ${quote(org.id)}, ${orgAdmin}, ${now}
  WHERE NOT EXISTS (SELECT 1 FROM "Entity"."OrganizationUsers" WHERE "OrganizationId" = ${quote(org.id)} AND "UserId" = ${orgAdmin});
INSERT INTO "Entity"."OrganizationProviderTypes"("Id","OrganizationId","ProviderTypeId","DateCreated")
  SELECT gen_random_uuid(), ${quote(org.id)}, t."Id", ${now} FROM "Entity"."OrganizationProviderType" t
  WHERE NOT EXISTS (SELECT 1 FROM "Entity"."OrganizationProviderTypes" x WHERE x."OrganizationId" = ${quote(org.id)} AND x."ProviderTypeId" = t."Id");`);
}

for (const o of listings) {
  const start = o.start ?? `${isoDay(dayOffset(base, o.startOffsetDays))}T00:00:00+00:00`;
  const [count, interval] = o.commitment ?? [null, null];
  out.push(`INSERT INTO "Opportunity"."Opportunity"("Id","Title","Description","TypeId","OrganizationId","Summary","Instructions","URL","Provider","ZltoReward","RewardType","VerificationEnabled",
  "CommitmentIntervalId","CommitmentIntervalCount","ParticipantCount","StatusId","Keywords","DateStart","DateEnd","CredentialIssuanceEnabled","Featured","EngagementTypeId","ShareWithPartners","Hidden",
  "DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
VALUES (${quote(o.id)}, ${quote(o.title)}, ${quote(o.description)}, (SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"=${quote(o.type)}), ${quote(o.organization.id)},
  ${quote(o.summary)}, ${quote(o.instructions)}, ${quote(o.url)}, ${quote(o.organization.name)}, ${o.zlto > 0 ? o.zlto : "NULL"}, ${quote(o.zlto > 0 ? "ZLTO" : "None")}, FALSE,
  ${interval ? `(SELECT "Id" FROM "Lookup"."TimeInterval" WHERE "Name"=${quote(interval)})` : "NULL"}, ${count ?? "NULL"}, 0,
  (SELECT "Id" FROM "Opportunity"."OpportunityStatus" WHERE "Name"='Active'), ${quote(`sample data,bbs journey,${o.sharedId}`)}, ${quote(start)}, ${quote(o.end)}, FALSE, FALSE,
  (SELECT "Id" FROM "Lookup"."EngagementType" WHERE "Name"=${quote(o.engagement)}), FALSE, FALSE, ${now}, ${orgAdmin}, ${now}, ${orgAdmin})
ON CONFLICT ("Id") DO UPDATE SET "Title" = EXCLUDED."Title", "Description" = EXCLUDED."Description", "TypeId" = EXCLUDED."TypeId", "OrganizationId" = EXCLUDED."OrganizationId",
  "Summary" = EXCLUDED."Summary", "Instructions" = EXCLUDED."Instructions", "URL" = EXCLUDED."URL", "Provider" = EXCLUDED."Provider", "ZltoReward" = EXCLUDED."ZltoReward",
  "RewardType" = EXCLUDED."RewardType", "CommitmentIntervalId" = EXCLUDED."CommitmentIntervalId", "CommitmentIntervalCount" = EXCLUDED."CommitmentIntervalCount",
  "StatusId" = EXCLUDED."StatusId", "Keywords" = EXCLUDED."Keywords", "DateStart" = EXCLUDED."DateStart", "DateEnd" = EXCLUDED."DateEnd",
  "EngagementTypeId" = EXCLUDED."EngagementTypeId", "Hidden" = FALSE, "DateModified" = ${now};`);
}

// Child rows: replaced, so a second run leaves the same set.
out.push(`DELETE FROM "Opportunity"."OpportunityCategories" WHERE "OpportunityId" IN (${ids(listings)});
DELETE FROM "Opportunity"."OpportunityCountries" WHERE "OpportunityId" IN (${ids(listings)});
DELETE FROM "Opportunity"."OpportunityLanguages" WHERE "OpportunityId" IN (${ids(listings)});
DELETE FROM "Opportunity"."OpportunitySkills" WHERE "OpportunityId" IN (${ids(listings)});`);
for (const o of listings) {
  out.push(`INSERT INTO "Opportunity"."OpportunityCategories"("Id","OpportunityId","CategoryId","DateCreated")
  SELECT gen_random_uuid(), ${quote(o.id)}, "Id", ${now} FROM "Opportunity"."OpportunityCategory" WHERE "Name"=${quote(o.category)};
INSERT INTO "Opportunity"."OpportunityCountries"("Id","OpportunityId","CountryId","DateCreated","DateModified") VALUES (gen_random_uuid(), ${quote(o.id)}, ${nigeria}, ${now}, ${now});
INSERT INTO "Opportunity"."OpportunityLanguages"("Id","OpportunityId","LanguageId","DateCreated")
  SELECT gen_random_uuid(), ${quote(o.id)}, "Id", ${now} FROM "Lookup"."Language" WHERE "CodeAlpha2" IN (${o.languages.map(quote).join(", ")});`);
  if (o.skills.length) {
    out.push(`INSERT INTO "Opportunity"."OpportunitySkills"("Id","OpportunityId","SkillId","DateCreated")
  SELECT gen_random_uuid(), ${quote(o.id)}, s.id, ${now} FROM (SELECT DISTINCT ON (lower("Name")) "Id" AS id FROM "Lookup"."Skill" WHERE lower("Name") IN (${o.skills.map((s) => quote(s.toLowerCase())).join(", ")}) ORDER BY lower("Name"), "DateCreated") s;`);
  }
}

// Halima's Yoma account, as the other demo users have one: Kaduna, Nigeria, 20, settings answered.
out.push(`INSERT INTO "Entity"."User"("Id","Email","EmailConfirmed","FirstName","Surname","DisplayName","CountryId","EducationId","GenderId","DateOfBirth","Region","City","LocationSource",
  "YoIDOnboarded","DateYoIDOnboarded","Settings","DateCreated","DateModified")
SELECT gen_random_uuid(), ${quote(halima.email)}, TRUE, ${quote(halima.first)}, ${quote(halima.last)}, ${quote(`${halima.first} ${halima.last}`)}, ${nigeria},
  (SELECT "Id" FROM "Lookup"."Education" WHERE "Name" LIKE 'Secondary%' ORDER BY "Name" LIMIT 1), (SELECT "Id" FROM "Lookup"."Gender" WHERE "Name"='Female'),
  CURRENT_DATE - INTERVAL '20 years', 'Kaduna State', 'Kaduna North', 'Manual', TRUE, ${now}, '{"User_Settings_Configured":true,"User_RumConsent":false}', ${now}, ${now}
WHERE NOT EXISTS (SELECT 1 FROM "Entity"."User" WHERE "Email" = ${quote(halima.email)});
-- She saves the solar job in the demo, so it starts unsaved.
DELETE FROM "Opportunity"."MyOpportunity" WHERE "UserId" = ${halimaUser} AND "OpportunityId" = ${quote(opportunityIds["opp-job-001"])}
  AND "ActionId" = (SELECT "Id" FROM "Opportunity"."MyOpportunityAction" WHERE "Name"='Saved');`);

out.push(`COMMIT;`);
console.log(out.join("\n"));
console.error(`${organizations.length} journey organisations and ${listings.length} journey listings (fictional sample data), and Halima's account.`);
