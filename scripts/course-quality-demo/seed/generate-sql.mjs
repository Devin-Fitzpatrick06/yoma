// Prints the demo seed as SQL, for psql to apply to a freshly migrated database.
// Local demo only: start.sh is the single caller, and nothing here ships in the API.
import { courses, demoLearners, organizations } from "./courses.mjs";
import { dayOffset, isoDay } from "./dates.mjs";

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

for (const org of organizations) {
  out.push(`INSERT INTO "Entity"."Organization"("Id","Name","NameHashValue","WebsiteURL","PrimaryContactName","PrimaryContactEmail","PrimaryContactPhone","VATIN","TaxNumber","RegistrationNumber","City","CountryId","StreetAddress","Province","PostalCode","Tagline","Biography","StatusId","CommentApproval","DateStatusModified","DateCreated","CreatedByUserId","DateModified","ModifiedByUserId")
VALUES (${quote(org.id)}, ${quote(org.name)}, ENCODE(DIGEST(${quote(org.name)}, 'sha256'), 'hex'), 'https://www.yoma.world/', 'Demo Contact', 'demo@example.org', '+27125555555', 'GB123456789', '0123456789', '12345/28/14', 'Demo City',
  (SELECT "Id" FROM "Lookup"."Country" WHERE "CodeAlpha2"='ZA'), 'Demo Street 1', 'Demo Province', '0000', ${quote(org.tagline)}, ${quote(org.tagline)},
  (SELECT "Id" FROM "Entity"."OrganizationStatus" WHERE "Name"='Active'), 'Approved', ${now}, ${now}, ${orgAdmin}, ${now}, ${orgAdmin});
INSERT INTO "Entity"."OrganizationUsers"("Id","OrganizationId","UserId","DateCreated") VALUES (gen_random_uuid(), ${quote(org.id)}, ${orgAdmin}, ${now});
INSERT INTO "Entity"."OrganizationProviderTypes"("Id","OrganizationId","ProviderTypeId","DateCreated")
  SELECT gen_random_uuid(), ${quote(org.id)}, "Id", ${now} FROM "Entity"."OrganizationProviderType";`);
}

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
  (SELECT "Id" FROM "Lookup"."EngagementType" WHERE "Name"='Online'), FALSE, ${now}, ${orgAdmin}, ${now}, ${orgAdmin});
INSERT INTO "Opportunity"."OpportunityCategories"("Id","OpportunityId","CategoryId","DateCreated")
  VALUES (gen_random_uuid(), ${quote(course.id)}, (SELECT "Id" FROM "Opportunity"."OpportunityCategory" WHERE "Name"=${quote(course.category)}), ${now});
INSERT INTO "Opportunity"."OpportunityCountries"("Id","OpportunityId","CountryId","DateCreated","DateModified")
  VALUES (gen_random_uuid(), ${quote(course.id)}, ${country(course.country ?? "WW")}, ${now}, ${now});
INSERT INTO "Opportunity"."OpportunityLanguages"("Id","OpportunityId","LanguageId","DateCreated")
  SELECT gen_random_uuid(), ${quote(course.id)}, "Id", ${now} FROM "Lookup"."Language" WHERE "CodeAlpha2"='EN';`);
}

// Views and completions, read by the "Trending & Popular" section. Each learner has one row per course and action,
// so a count cannot exceed the number of demo learners. Dates fall after the course started, and differ between rows.
const engaged = courses.filter((course) => course.views || course.completions);
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

  // Search reads a course's completed count from this column, not from the rows above.
  if (course.completions) out.push(`UPDATE "Opportunity"."Opportunity" SET "ParticipantCount" = ${course.completions} WHERE "Id" = ${quote(course.id)};`);
}

out.push(`COMMIT;`);
console.log(out.join("\n"));
