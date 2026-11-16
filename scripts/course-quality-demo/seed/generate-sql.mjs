// Prints the demo seed as SQL, for psql to apply to a freshly migrated database: the accounts, every live Yoma Learning
// listing from .cache/live-catalogue.json with scaled demo participation, and the named walkthrough courses 101 to 108.
// Local demo only: start.sh is the single caller, and nothing here ships in the API.
import { courses, demoLearners, organizations } from "./courses.mjs";
import { dayOffset, isoDay } from "./dates.mjs";
import { learnerIndex, loadCatalogue, participation, random } from "./live.mjs";

const base = process.env.DEMO_BASE_DATE;
const useFixtures = process.argv.includes("--fixtures");
if (!base) throw new Error("DEMO_BASE_DATE is not set");

const quote = (value) => (value === null || value === undefined ? "NULL" : `'${String(value).replaceAll("'", "''")}'`);
const fixtureUrl = (course) => `http://demo-fixtures.localtest.me/courses/${course.fixture}`;
const now = `(CURRENT_TIMESTAMP AT TIME ZONE 'UTC')`;
const orgAdmin = `(SELECT "Id" FROM "Entity"."User" WHERE "Email" = 'testorgadminuser@gmail.com')`;
const country = (code) => `(SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"=${quote(code)})`;
const stamp = (daysAgo, hour) => quote(`${isoDay(dayOffset(base, -daysAgo))} ${String(hour).padStart(2, "0")}:00:00+00`);

const out = [`\\set ON_ERROR_STOP on`, `BEGIN;`, `CREATE EXTENSION IF NOT EXISTS "pgcrypto";`];

// --named-only: for a start from the saved snapshot. Puts the named courses' links and dates back to their seed values
// for today's base date, so they match today's demo pages, and leaves everything else as saved.
if (process.argv.includes("--named-only")) {
  for (const course of courses) {
    const url = !useFixtures && course.liveUrl ? course.liveUrl : fixtureUrl(course);
    const end = course.endOffsetDays === null ? null : `${isoDay(dayOffset(base, course.endOffsetDays))} 23:59:59.999+00`;
    out.push(`UPDATE "Opportunity"."Opportunity" SET "URL" = ${quote(url)}, "DateStart" = ${quote(`${isoDay(dayOffset(base, course.startOffsetDays))} 00:00:00+00`)},
  "DateEnd" = ${quote(end)}, "DateModified" = ${now} WHERE "Id" = ${quote(course.id)};`);
  }
  out.push(`COMMIT;`);
  console.log(out.join("\n"));
  process.exit(0);
}

// Settings already answered, so the first-login dialogs do not cover the demo screens.
const settings = `'{"User_Settings_Configured":true,"User_RumConsent":false}'`;
// The learner is in the United States so that "Opportunities in United States" shows when signed in.
const users = [
  { email: "testuser@gmail.com", first: "Test", surname: "User", age: 20, country: "US" },
  { email: "testadminuser@gmail.com", first: "Test Admin", surname: "User", age: 21, country: "ZA" },
  { email: "testorgadminuser@gmail.com", first: "Test Organization Admin", surname: "User", age: 22, country: "ZA" },
  ...demoLearners.map((learner) => ({ ...learner, first: "Demo" })),
];
for (const { email, first, surname, age, country: code } of users) {
  out.push(`INSERT INTO "Entity"."User"("Id","Email","EmailConfirmed","FirstName","Surname","DisplayName","CountryId","EducationId","GenderId","DateOfBirth","YoIDOnboarded","DateYoIDOnboarded","Settings","DateCreated","DateModified")
VALUES (gen_random_uuid(), ${quote(email)}, TRUE, ${quote(first)}, ${quote(surname)}, ${quote(`${first} ${surname}`)},
  ${country(code)}, (SELECT "Id" FROM "Lookup"."Education" ORDER BY "Name" LIMIT 1),
  (SELECT "Id" FROM "Lookup"."Gender" ORDER BY "Name" LIMIT 1), CURRENT_DATE - INTERVAL '${age} years', TRUE, ${now}, ${settings}, ${now}, ${now});`);
}

const partnerId = organizations.find((o) => o.key === "partner").id;
const demoAdmin = `(SELECT "Id" FROM "Entity"."User" WHERE "Email" = 'testadminuser@gmail.com')`;
const learnerUser = (no) => `(SELECT "Id" FROM "Entity"."User" WHERE "Email"='demo.learner${String(no).padStart(2, "0")}@example.org')`;

for (const org of organizations.filter((o) => !o.live)) {
  out.push(`INSERT INTO "Entity"."Organization"("Id","Name","NameHashValue","WebsiteURL","PrimaryContactName","PrimaryContactEmail","PrimaryContactPhone","VATIN","TaxNumber","RegistrationNumber","City","CountryId","StreetAddress","Province","PostalCode","Tagline","Biography","StatusId","CommentApproval","DateStatusModified","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
VALUES (${quote(org.id)}, ${quote(org.name)}, ENCODE(DIGEST(${quote(org.name)}, 'sha256'), 'hex'), 'https://www.yoma.world/', 'Demo Contact', 'demo@example.org', '+27125555555', 'GB123456789', '0123456789', '12345/28/14', 'Demo City',
  (SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"='ZA'), 'Demo Street 1', 'Demo Province', '0000', ${quote(org.tagline)}, ${quote(org.tagline)},
  (SELECT "Id" FROM "Entity"."OrganizationStatus" WHERE "Name"='Active'), 'Approved', ${now}, ${now}, ${orgAdmin}, ${now}, ${orgAdmin});
INSERT INTO "Entity"."OrganizationUsers"("Id","OrganizationId","UserId","DateCreated") VALUES (gen_random_uuid(), ${quote(org.id)}, ${orgAdmin}, ${now});
INSERT INTO "Entity"."OrganizationProviderTypes"("Id","OrganizationId","ProviderTypeId","DateCreated")
  SELECT gen_random_uuid(), ${quote(org.id)}, "Id", ${now} FROM "Entity"."OrganizationProviderType";`);
}

// The demo admin is also the partner's admin, so moving between the reviewer and partner views needs no new sign-in.
out.push(`INSERT INTO "Entity"."OrganizationUsers"("Id","OrganizationId","UserId","DateCreated") VALUES (gen_random_uuid(), ${quote(partnerId)}, ${demoAdmin}, ${now});`);

out.push(...liveCatalogueSql());

for (const course of courses) {
  const organization = organizations.find((o) => o.key === course.organization);
  const url = !useFixtures && course.liveUrl ? course.liveUrl : fixtureUrl(course);
  const end = course.endOffsetDays === null ? null : `${isoDay(dayOffset(base, course.endOffsetDays))} 23:59:59.999+00`;
  const { summary } = course;
  const [count, interval] = course.commitment;

  out.push(`INSERT INTO "Opportunity"."Opportunity"("Id","Title","Description","TypeId","OrganizationId","Summary","Instructions","URL","Provider","RewardType","VerificationEnabled","CommitmentIntervalId","CommitmentIntervalCount","StatusId","Keywords","DateStart","DateEnd","CredentialIssuanceEnabled","Featured","EngagementTypeId","Hidden","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
VALUES (${quote(course.id)}, ${quote(course.title)}, ${quote(`${summary} A sample listing for the Course Quality prototype.`)},
  (SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"='Learning'), ${quote(organization.id)}, ${quote(summary)}, 'Open the course link to start.', ${quote(url)}, ${quote(course.provider)},
  'None', FALSE, (SELECT "Id" FROM "Lookup"."TimeInterval" WHERE "Name"=${quote(interval)}), ${count},
  (SELECT "Id" FROM "Opportunity"."OpportunityStatus" WHERE "Name"='Active'), 'demo', ${quote(`${isoDay(dayOffset(base, course.startOffsetDays))} 00:00:00+00`)}, ${quote(end)}, FALSE, ${course.featured ? "TRUE" : "FALSE"},
  (SELECT "Id" FROM "Lookup"."EngagementType" WHERE "Name"='Remote'), FALSE, ${now}, ${orgAdmin}, ${now}, ${orgAdmin});
INSERT INTO "Opportunity"."OpportunityCategories"("Id","OpportunityId","CategoryId","DateCreated")
  VALUES (gen_random_uuid(), ${quote(course.id)}, (SELECT "Id" FROM "Opportunity"."OpportunityCategory" WHERE "Name"=${quote(course.category)}), ${now});
INSERT INTO "Opportunity"."OpportunityCountries"("Id","OpportunityId","CountryId","DateCreated","DateModified")
  VALUES (gen_random_uuid(), ${quote(course.id)}, ${country(course.country ?? "WW")}, ${now}, ${now});
INSERT INTO "Opportunity"."OpportunityLanguages"("Id","OpportunityId","LanguageId","DateCreated")
  SELECT gen_random_uuid(), ${quote(course.id)}, "Id", ${now} FROM "Lookup"."Language" WHERE "CodeAlpha2"='EN';`);
}

// Views and completions, read by the "Trending & Popular" section. Each learner has one row per course and action,
// so a count cannot exceed the number of demo learners. Dates fall after the course started, and differ between rows.
const engaged = courses.filter((course) => course.views || course.completions || course.started);
for (const [courseIndex, course] of engaged.entries()) {
  const user = (learner) => `(SELECT "Id" FROM "Entity"."User" WHERE "Email"=${quote(learner.email)})`;
  const daysAgo = (rowIndex) => 1 + ((courseIndex * 2 + rowIndex * 3) % Math.min(21, -course.startOffsetDays));

  demoLearners.slice(0, course.views ?? 0).forEach((learner, i) => {
    const when = stamp(daysAgo(i), 8 + i);
    out.push(`INSERT INTO "Opportunity"."MyOpportunity"("Id","UserId","OpportunityId","ActionId","DateCreated","DateModified")
VALUES (gen_random_uuid(), ${user(learner)}, ${quote(course.id)}, (SELECT "Id" FROM "Opportunity"."MyOpportunityAction" WHERE "Name"='Viewed'), ${when}, ${when});`);
  });

  demoLearners.slice(0, course.completions ?? 0).forEach((learner, i) => {
    const when = stamp(daysAgo(i), 9 + i);
    out.push(`INSERT INTO "Opportunity"."MyOpportunity"("Id","UserId","OpportunityId","ActionId","VerificationStatusId","DateStart","DateEnd","DateCompleted","DateCreated","DateModified")
VALUES (gen_random_uuid(), ${user(learner)}, ${quote(course.id)}, (SELECT "Id" FROM "Opportunity"."MyOpportunityAction" WHERE "Name"='Verification'),
  (SELECT "Id" FROM "Opportunity"."MyOpportunityVerificationStatus" WHERE "Name"='Completed'), ${quote(`${isoDay(dayOffset(base, course.startOffsetDays))} 00:00:00+00`)}, ${when}, ${when}, ${when}, ${when});`);
  });

  // Started but unfinished: clicked through to the course, and not among the learners who completed it.
  demoLearners.slice(course.completions ?? 0, (course.completions ?? 0) + (course.started ?? 0)).forEach((learner, i) => {
    const when = stamp(daysAgo(i), 10 + i);
    out.push(`INSERT INTO "Opportunity"."MyOpportunity"("Id","UserId","OpportunityId","ActionId","DateCreated","DateModified")
VALUES (gen_random_uuid(), ${user(learner)}, ${quote(course.id)}, (SELECT "Id" FROM "Opportunity"."MyOpportunityAction" WHERE "Name"='NavigatedExternalLink'), ${when}, ${when});`);
  });

  // Search reads a course's completed count from this column, not from the rows above.
  if (course.completions) out.push(`UPDATE "Opportunity"."Opportunity" SET "ParticipantCount" = ${course.completions} WHERE "Id" = ${quote(course.id)};`);
}

// Skills refine the example roles on a course's learner card.
const skillNames = [...new Set(courses.flatMap((course) => course.skills ?? []))];
for (const name of skillNames) {
  out.push(`INSERT INTO "Lookup"."Skill"("Id","Name","ExternalId","DateCreated","DateModified")
VALUES (gen_random_uuid(), ${quote(name)}, ${quote(`demo-${name}`)}, ${now}, ${now}) ON CONFLICT DO NOTHING;`);
}
for (const course of courses) {
  for (const name of course.skills ?? []) {
    out.push(`INSERT INTO "Opportunity"."OpportunitySkills"("Id","OpportunityId","SkillId","DateCreated")
SELECT gen_random_uuid(), ${quote(course.id)}, "Id", ${now} FROM "Lookup"."Skill" WHERE "Name"=${quote(name)} LIMIT 1;`);
  }
}

out.push(...liveParticipationSql());
out.push(`COMMIT;`);
console.log(out.join("\n"));

// ---- Live catalogue ----
// The listings go into a temporary table as JSON, then set-based inserts map their names onto this database's lookups.
// Live ids are kept. Skills that the database lacks are added under their live ids.
function liveItems() {
  liveItems.cache ??= loadCatalogue().items;
  return liveItems.cache;
}

function liveCatalogueSql() {
  const keep = ["id", "title", "description", "organizationId", "organizationName", "summary", "instructions", "url", "zltoReward", "zltoRewardCumulative",
    "verificationEnabled", "verificationMethod", "commitmentInterval", "commitmentIntervalCount", "participantLimit", "keywords", "dateStart", "dateEnd",
    "featured", "engagementType", "shareWithPartners", "categories", "countries", "languages", "skills", "verificationTypes"];
  const sql = [`CREATE TEMP TABLE live_item(d jsonb NOT NULL) ON COMMIT DROP;`];
  const items = liveItems();
  for (let i = 0; i < items.length; i += 200) {
    const rows = items.slice(i, i + 200).map((item) => `(${quote(JSON.stringify(Object.fromEntries(keep.map((k) => [k, item[k] ?? null]))))}::jsonb)`);
    sql.push(`INSERT INTO live_item VALUES\n${rows.join(",\n")};`);
  }
  sql.push(`
INSERT INTO "Entity"."Organization"("Id","Name","NameHashValue","WebsiteURL","PrimaryContactName","PrimaryContactEmail","PrimaryContactPhone","VATIN","TaxNumber","RegistrationNumber","City","CountryId","StreetAddress","Province","PostalCode","Tagline","Biography","StatusId","CommentApproval","DateStatusModified","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT DISTINCT ON (d->>'organizationId') (d->>'organizationId')::uuid, d->>'organizationName', ENCODE(DIGEST(d->>'organizationName', 'sha256'), 'hex'), 'https://www.yoma.world/',
  'Demo Contact', 'demo@example.org', '+27125555555', 'GB123456789', '0123456789', '12345/28/14', 'Demo City', (SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"='ZA'),
  'Demo Street 1', 'Demo Province', '0000', 'Live Yoma organisation, imported for the Course Quality demo', 'Live Yoma organisation, imported for the Course Quality demo. Contact details are placeholders.',
  (SELECT "Id" FROM "Entity"."OrganizationStatus" WHERE "Name"='Active'), 'Approved', ${now}, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_item ORDER BY d->>'organizationId';
INSERT INTO "Entity"."OrganizationProviderTypes"("Id","OrganizationId","ProviderTypeId","DateCreated")
  SELECT gen_random_uuid(), o.id, t."Id", ${now} FROM (SELECT DISTINCT (d->>'organizationId')::uuid AS id FROM live_item) o CROSS JOIN "Entity"."OrganizationProviderType" t;

CREATE TEMP TABLE skill_map ON COMMIT DROP AS SELECT DISTINCT ON (lower("Name")) lower("Name") AS k, "Id" AS id FROM "Lookup"."Skill" ORDER BY lower("Name"), "DateCreated";
CREATE UNIQUE INDEX ON skill_map(k);
INSERT INTO "Lookup"."Skill"("Id","Name","InfoURL","ExternalId","DateCreated","DateModified")
  SELECT DISTINCT ON (lower(s->>'name')) (s->>'id')::uuid, s->>'name', s->>'infoURL', COALESCE(substring(s->>'infoURL' from '[^/]+$'), s->>'id'), ${now}, ${now}
  FROM live_item, jsonb_array_elements(COALESCE(d->'skills', '[]')) s WHERE NOT EXISTS (SELECT 1 FROM skill_map m WHERE m.k = lower(s->>'name'))
  ORDER BY lower(s->>'name') ON CONFLICT DO NOTHING;
INSERT INTO skill_map SELECT DISTINCT ON (lower("Name")) lower("Name"), "Id" FROM "Lookup"."Skill" ORDER BY lower("Name"), "DateCreated" ON CONFLICT DO NOTHING;

INSERT INTO "Opportunity"."Opportunity"("Id","Title","Description","TypeId","OrganizationId","Summary","Instructions","URL","ZltoReward","ZltoRewardCumulative","RewardType","VerificationEnabled","VerificationMethod",
  "CommitmentIntervalId","CommitmentIntervalCount","ParticipantLimit","ParticipantCount","StatusId","Keywords","DateStart","DateEnd","CredentialIssuanceEnabled","Featured","EngagementTypeId","ShareWithPartners","Hidden",
  "DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
SELECT (d->>'id')::uuid, d->>'title', d->>'description', (SELECT "Id" FROM "Opportunity"."OpportunityType" WHERE "Name"='Learning'), (d->>'organizationId')::uuid,
  d->>'summary', d->>'instructions', NULLIF(btrim(d->>'url'), ''), (d->>'zltoReward')::numeric, (d->>'zltoRewardCumulative')::numeric,
  CASE WHEN (d->>'zltoReward')::numeric > 0 THEN 'ZLTO' ELSE 'None' END, (d->>'verificationEnabled')::boolean, d->>'verificationMethod',
  ti."Id", (d->>'commitmentIntervalCount')::smallint, (d->>'participantLimit')::int, 0, (SELECT "Id" FROM "Opportunity"."OpportunityStatus" WHERE "Name"='Active'),
  (SELECT string_agg(k, ',') FROM jsonb_array_elements_text(COALESCE(d->'keywords', '[]')) k), (d->>'dateStart')::timestamptz, (d->>'dateEnd')::timestamptz, FALSE,
  COALESCE((d->>'featured')::boolean, FALSE), et."Id", COALESCE((d->>'shareWithPartners')::boolean, FALSE), FALSE, ${now}, ${orgAdmin}, ${now}, ${orgAdmin}
FROM live_item
  LEFT JOIN "Lookup"."TimeInterval" ti ON ti."Name" = d->>'commitmentInterval'
  -- live Yoma says Online and Offline, this database Remote and OnSite
  LEFT JOIN "Lookup"."EngagementType" et ON et."Name" = CASE d->>'engagementType' WHEN 'Online' THEN 'Remote' WHEN 'Offline' THEN 'OnSite' ELSE d->>'engagementType' END;

-- By id, as this branch's category migration does: it renamed eight live categories in place and merged two into others.
INSERT INTO "Opportunity"."OpportunityCategories"("Id","OpportunityId","CategoryId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM live_item, jsonb_array_elements(d->'categories') c
  LEFT JOIN (VALUES ('d0d322ab-d1d7-44b6-94e8-7b85246aa42e'::uuid, '2ccbacf7-1ed9-4e20-bb7c-43edfdb3f950'::uuid),
    ('1dc39a5d-e049-4cfe-b708-855fce97b86e'::uuid, 'fa564c1c-591a-4a6d-8294-20165da8866b'::uuid)) merged(live_id, into_id) ON merged.live_id = (c->>'id')::uuid
  JOIN "Opportunity"."OpportunityCategory" k ON k."Id" = COALESCE(merged.into_id, (c->>'id')::uuid)) x;
INSERT INTO "Opportunity"."OpportunityCountries"("Id","OpportunityId","CountryId","DateCreated","DateModified")
  SELECT gen_random_uuid(), x.oid, x.cid, ${now}, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS cid FROM live_item, jsonb_array_elements(d->'countries') c
  JOIN "Lookup"."Country" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunityLanguages"("Id","OpportunityId","LanguageId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.lid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, k."Id" AS lid FROM live_item, jsonb_array_elements(d->'languages') c
  JOIN "Lookup"."Language" k ON k."CodeAlpha2" = c->>'codeAlpha2') x;
INSERT INTO "Opportunity"."OpportunitySkills"("Id","OpportunityId","SkillId","DateCreated")
  SELECT gen_random_uuid(), x.oid, x.sid, ${now} FROM (SELECT DISTINCT (d->>'id')::uuid AS oid, m.id AS sid FROM live_item, jsonb_array_elements(COALESCE(d->'skills', '[]')) s
  JOIN skill_map m ON m.k = lower(s->>'name')) x;
INSERT INTO "Opportunity"."OpportunityVerificationTypes"("Id","OpportunityId","VerificationTypeId","Description","DateCreated","DateModified")
  SELECT gen_random_uuid(), (d->>'id')::uuid, k."Id", left(v->>'description', 255), ${now}, ${now} FROM live_item, jsonb_array_elements(COALESCE(d->'verificationTypes', '[]')) v
  JOIN "Opportunity"."OpportunityVerificationType" k ON k."Name" = v->>'type';`);
  return sql;
}

// Scaled demo participation for the live listings (the formula is in live.mjs). One row per learner, course and action.
function liveParticipationSql() {
  const sql = [`CREATE TEMP TABLE live_activity(opportunity uuid, learner int, action text, at timestamptz) ON COMMIT DROP;`];
  const counts = [];
  const rows = [];
  const today = dayOffset(base, 0);
  for (const item of liveItems()) {
    const { started, completed, viewed } = participation(item);
    if (!viewed) continue;
    const begin = Math.max(Date.parse(item.dateStart), today - 540 * 86_400_000);
    const span = Math.max(1, Math.floor((today - begin) / 86_400_000));
    if (begin >= today) continue;
    const at = (i, salt) => quote(new Date(today - (1 + Math.floor(random(item.id, salt, i) * span)) * 86_400_000 + (8 + (i % 10)) * 3_600_000).toISOString());
    for (let i = 0; i < viewed; i++) {
      const learner = learnerIndex(item, i, demoLearners.length) + 1;
      rows.push(`(${quote(item.id)}, ${learner}, 'Viewed', ${at(i, "viewed")})`);
      if (i < started) rows.push(`(${quote(item.id)}, ${learner}, 'NavigatedExternalLink', ${at(i, "left")})`);
      if (i < completed) rows.push(`(${quote(item.id)}, ${learner}, 'Verification', ${at(i, "left")})`);
    }
    if (completed) counts.push(`(${quote(item.id)}, ${completed})`);
  }
  for (let i = 0; i < rows.length; i += 1000) sql.push(`INSERT INTO live_activity VALUES\n${rows.slice(i, i + 1000).join(",\n")};`);
  // A completion is dated a few days after the learner left for the course.
  sql.push(`
INSERT INTO "Opportunity"."MyOpportunity"("Id","UserId","OpportunityId","ActionId","VerificationStatusId","DateStart","DateEnd","DateCompleted","DateCreated","DateModified")
SELECT gen_random_uuid(), u."Id", a.opportunity, act."Id",
  CASE WHEN a.action = 'Verification' THEN (SELECT "Id" FROM "Opportunity"."MyOpportunityVerificationStatus" WHERE "Name"='Completed') END,
  CASE WHEN a.action = 'Verification' THEN a.at END,
  CASE WHEN a.action = 'Verification' THEN LEAST(a.at + INTERVAL '3 days', ${now}) END,
  CASE WHEN a.action = 'Verification' THEN LEAST(a.at + INTERVAL '3 days', ${now}) END,
  CASE WHEN a.action = 'Verification' THEN LEAST(a.at + INTERVAL '3 days', ${now}) ELSE a.at END,
  CASE WHEN a.action = 'Verification' THEN LEAST(a.at + INTERVAL '3 days', ${now}) ELSE a.at END
FROM live_activity a
  JOIN "Entity"."User" u ON u."Email" = 'demo.learner' || lpad(a.learner::text, 2, '0') || '@example.org'
  JOIN "Opportunity"."MyOpportunityAction" act ON act."Name" = a.action;`);
  if (counts.length) sql.push(`UPDATE "Opportunity"."Opportunity" o SET "ParticipantCount" = v.n FROM (VALUES ${counts.join(", ")}) AS v(id, n) WHERE o."Id" = v.id::uuid;`);
  return sql;
}
