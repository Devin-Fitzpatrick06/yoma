-- Zeki against Yoma's existing review, on one database. Read-only: one temporary view, dropped with the session.
-- Run through compare-review.sh. See docs/work/active/course-quality-judger/zeki-vs-current-review.md for what each figure means.
--
-- "Approved by the current system" = what a learner can find today: a Learning listing whose status is Active, whose organisation
-- an admin approved (Active), and which is not hidden. The current system has no other gate on a listing (see the doc).
-- "Held back by Zeki" = an approved listing whose latest Zeki assessment is not Verified (Not verified, or waiting for review).
--
-- psql variables: list_limit (rows in the course list, default 50), csv (1 = print only the full list as CSV).

\set ON_ERROR_STOP on
\if :{?list_limit}
\else
  \set list_limit 50
\endif
\if :{?csv}
\else
  \set csv 0
\endif
\pset footer off

CREATE TEMP VIEW zeki_compare AS
WITH approved AS (
  SELECT o."Id", o."Title", org."Name" AS "Organization", o."DateEnd", o."URL"
  FROM "Opportunity"."Opportunity" o
  JOIN "Opportunity"."OpportunityType" t ON t."Id" = o."TypeId" AND t."Name" = 'Learning'
  JOIN "Opportunity"."OpportunityStatus" s ON s."Id" = o."StatusId" AND s."Name" = 'Active'
  JOIN "Entity"."Organization" org ON org."Id" = o."OrganizationId"
  JOIN "Entity"."OrganizationStatus" os ON os."Id" = org."StatusId" AND os."Name" = 'Active'
  WHERE COALESCE(o."Hidden", false) = false
),
criteria AS (
  SELECT a."OpportunityId",
    max(CASE WHEN c->>'Criterion' = 'ListingLive' THEN (c->>'Score')::int END) AS live_score,
    bool_or(c->>'Criterion' = 'LinkWorks' AND (c->>'HardGateFailed')::boolean) AS link_failed,
    max(CASE WHEN c->>'Criterion' = 'LinkWorks' THEN (c->>'Score')::int END) AS link_score,
    bool_or(c->>'Criterion' = 'DatesAccurate' AND (c->>'HardGateFailed')::boolean) AS dates_failed,
    max(CASE WHEN c->>'Criterion' = 'DataComplete' THEN (c->>'Score')::int END) AS data_score
  FROM "CourseQuality"."Assessment" a
  CROSS JOIN LATERAL jsonb_array_elements(a."Criteria") c
  WHERE a."IsLatest"
  GROUP BY a."OpportunityId"
)
SELECT ap."Id", ap."Title", ap."Organization", ap."DateEnd",
  a."Status" AS zeki_status, a."ReviewStatus" AS review_status, a."Format" AS format, g."Grade" AS grade,
  a."Id" IS NOT NULL AS assessed,
  COALESCE(a."Status" <> 'Verified', false) AS held_back,
  -- the failure types of the README's Problem section; a listing can have several
  COALESCE(cr.live_score < 100, false) AS ended_or_not_live,
  COALESCE(ap."DateEnd" < now(), false) AS past_end_date,
  COALESCE(cr.link_failed, false) AS broken_link,
  COALESCE(NOT cr.link_failed AND cr.link_score < 100, false) AS link_unconfirmed,
  COALESCE(cr.dates_failed, false) AS placeholder_dates,
  COALESCE(cr.data_score < 50, false)
    OR COALESCE(a."ReviewReasons" LIKE '%does not clearly match%' OR a."ReviewReasons" LIKE '%may not be this listing%', false) AS page_mismatch,
  COALESCE(a."Format" = 'Webinar', false) AS webinar,
  COALESCE(a."Format" = 'Unknown', false) AS format_unknown,
  COALESCE(a."ReviewReasons" LIKE '%could not be read%', false) AS page_unread
FROM approved ap
LEFT JOIN "CourseQuality"."Assessment" a ON a."OpportunityId" = ap."Id" AND a."IsLatest"
LEFT JOIN criteria cr ON cr."OpportunityId" = ap."Id"
LEFT JOIN "CourseQuality"."Grade" g ON g."OpportunityId" = ap."Id";

\if :csv
  \pset format csv
  SELECT "Id" AS opportunity_id, "Title" AS title, "Organization" AS organization, zeki_status, review_status, format, grade,
    ended_or_not_live, past_end_date, broken_link, link_unconfirmed, placeholder_dates, page_mismatch, format_unknown, page_unread
  FROM zeki_compare WHERE held_back ORDER BY "Organization", "Title";
\else
  \echo
  \echo '1. Approved by the current system (publicly visible Learning listings), and what Zeki says about them'
  SELECT count(*) AS approved,
    count(*) FILTER (WHERE assessed) AS assessed_by_zeki,
    count(*) FILTER (WHERE zeki_status = 'Verified') AS zeki_verified,
    count(*) FILTER (WHERE zeki_status = 'NeedsReview') AS zeki_waiting_for_review,
    count(*) FILTER (WHERE zeki_status = 'NotVerified') AS zeki_not_verified,
    count(*) FILTER (WHERE NOT assessed) AS not_assessed
  FROM zeki_compare;

  \echo '2. Approved but held back by Zeki, by failure type (a listing can count in several rows)'
  SELECT failure, count(*) AS held_back, count(*) FILTER (WHERE zeki_status = 'NotVerified') AS of_which_not_verified,
    count(*) FILTER (WHERE also_current) AS also_caught_by_the_expiry_job
  FROM (
    SELECT zeki_status, x.failure, x.hit, x.also_current
    FROM zeki_compare
    CROSS JOIN LATERAL (VALUES
      ('Expired or not live', ended_or_not_live, past_end_date),
      ('Broken link', broken_link, false),
      ('Link could not be confirmed', link_unconfirmed, false),
      ('Placeholder or incoherent dates', placeholder_dates, false),
      ('Page does not match the listing', page_mismatch, false),
      ('Format (course or webinar) unclear', format_unknown, false),
      ('Page could not be read', page_unread, false)) AS x(failure, hit, also_current)
    WHERE held_back AND x.hit
  ) t
  GROUP BY failure ORDER BY held_back DESC;

  \echo '3. Webinars listed as Learning: the current system has no format, so all of these show as courses today'
  SELECT count(*) FILTER (WHERE webinar) AS approved_webinars,
    count(*) FILTER (WHERE webinar AND held_back) AS of_which_held_back
  FROM zeki_compare;

  \echo '4. Grades of approved listings (relative bands among Verified courses; the current system has no quality ranking)'
  SELECT COALESCE(grade, 'not graded') AS grade, count(*) AS courses FROM zeki_compare GROUP BY 1 ORDER BY 1;

  \echo '5. Held back, by organisation'
  SELECT "Organization" AS organization, count(*) AS approved, count(*) FILTER (WHERE held_back) AS held_back,
    count(*) FILTER (WHERE zeki_status = 'NotVerified') AS not_verified, count(*) FILTER (WHERE zeki_status = 'NeedsReview') AS waiting_for_review
  FROM zeki_compare GROUP BY 1 HAVING count(*) FILTER (WHERE held_back) > 0 ORDER BY held_back DESC, 1;

  \echo '6. Courses the current system approves but Zeki holds back (Not verified first; --list N for more, --csv for all)'
  SELECT left("Title", 60) AS title, left("Organization", 30) AS organization, zeki_status,
    concat_ws(', ',
      CASE WHEN ended_or_not_live THEN 'expired' END, CASE WHEN broken_link THEN 'broken link' END,
      CASE WHEN link_unconfirmed THEN 'link unconfirmed' END, CASE WHEN placeholder_dates THEN 'dates' END,
      CASE WHEN page_mismatch THEN 'page mismatch' END, CASE WHEN format_unknown THEN 'format unclear' END,
      CASE WHEN page_unread THEN 'page unread' END) AS why
  FROM zeki_compare WHERE held_back
  ORDER BY zeki_status = 'NotVerified' DESC, "Organization", "Title"
  LIMIT :list_limit;
\endif
