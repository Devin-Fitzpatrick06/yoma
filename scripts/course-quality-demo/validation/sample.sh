#!/bin/bash
# Draws the hand-rated validation sample from the running demo database (or --database-url URL).
# Passes every other option to draw-sample.mjs: --seed N, --size 100, --min-non-alison 30, --out DIR.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$DIR/lib.sh"

URL=""
ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --database-url) URL="${2:?--database-url needs a URL}"; shift 2 ;;
    *) ARGS+=("$1"); shift ;;
  esac
done

# live Learning listings learners can see, with Zeki's latest result and grade
QUERY='SELECT COALESCE(json_agg(row_to_json(x)), '"'"'[]'"'"') FROM (
  SELECT o."Id" AS id, o."Title" AS title, org."Name" AS organization, o."URL" AS url, a."Status" AS status, g."Grade" AS grade
  FROM "Opportunity"."Opportunity" o
  JOIN "Opportunity"."OpportunityType" t ON t."Id" = o."TypeId" AND t."Name" = '"'"'Learning'"'"'
  JOIN "Opportunity"."OpportunityStatus" s ON s."Id" = o."StatusId" AND s."Name" = '"'"'Active'"'"'
  JOIN "Entity"."Organization" org ON org."Id" = o."OrganizationId"
  JOIN "Entity"."OrganizationStatus" os ON os."Id" = org."StatusId" AND os."Name" = '"'"'Active'"'"'
  LEFT JOIN "CourseQuality"."Assessment" a ON a."OpportunityId" = o."Id" AND a."IsLatest"
  LEFT JOIN "CourseQuality"."Grade" g ON g."OpportunityId" = o."Id"
  WHERE o."Hidden" IS NOT TRUE AND o."DateStart" <= now() AND (o."DateEnd" IS NULL OR o."DateEnd" > now())
  ORDER BY o."Id") x;'

if [ -n "$URL" ]; then
  psql "$URL" -At -X -c "$QUERY"
else
  [ -n "$(compose ps -q postgres 2>/dev/null)" ] || { echo "The demo is not running. Start it with ./start.sh first, or pass --database-url." >&2; exit 1; }
  sql -At -X -c "$QUERY"
fi | node "$DIR/validation/draw-sample.mjs" ${ARGS[@]+"${ARGS[@]}"}
