#!/bin/bash
# Starts the whole Course Quality demo with one command. See README.md.
#   ./start.sh              load the saved demo database if there is one (about 3 minutes); otherwise import every live
#                           course, assess a sample of 40 and open the website (about 8 minutes). Nothing is saved.
#   ./start.sh --full       with no saved database: assess and grade every live course (about 45 minutes), then save
#                           the database so later starts take minutes
#   ./start.sh --refresh    import, assess and grade everything again, even if a saved database exists
#   ./start.sh --sample N   import everything but assess only N live courses from mixed hosts; nothing is saved
#   ./start.sh --fixtures   use only the demo pages for the named courses 101 to 108
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
USE_FIXTURES=false
REFRESH=false
FULL=false
SAMPLE=""

while [ $# -gt 0 ]; do
  case "$1" in
    --fixtures) USE_FIXTURES=true ;;
    --refresh) REFRESH=true ;;
    --full) FULL=true ;;
    --sample)
      SAMPLE="${2:-}"
      [[ "$SAMPLE" =~ ^[1-9][0-9]*$ ]] || { echo "--sample needs a number of courses, for example --sample 40."; exit 2; }
      shift ;;
    *) echo "Unknown option: $1. The options are --full, --refresh, --sample N and --fixtures."; exit 2 ;;
  esac
  shift
done

FROM_SNAPSHOT=false
[ -f "$SNAPSHOT" ] && [ "$REFRESH" = false ] && [ -z "$SAMPLE" ] && FROM_SNAPSHOT=true
# The full check takes about 45 minutes, so without a saved database the default is a quick sample: --full opts in.
if [ "$FROM_SNAPSHOT" = false ] && [ -z "$SAMPLE" ] && [ "$FULL" = false ] && [ "$REFRESH" = false ]; then
  SAMPLE=40
  echo "No saved demo database here, so this is the quick start: 40 courses are checked and graded (about 8 minutes)."
  echo "For every course graded and a saved database, run ./start.sh --full once (about 45 minutes)."
fi

export DEMO_BASE_DATE
DEMO_BASE_DATE="$(date -u +%Y-%m-%d)"
export DEMO_NEXTAUTH_SECRET
DEMO_NEXTAUTH_SECRET="$(openssl rand -hex 32)"

stop_with() { printf '\nCould not start the demo.\n%s\n' "$1" >&2; exit 1; }

current_step="checking this Mac"
on_failure() {
  printf '\nThe demo stopped while %s.\n' "$current_step" >&2
  printf 'Details are in %s\n' "$LOG_DIR" >&2
  printf 'Run ./stop.sh to clean up, then try ./start.sh again.\n' >&2
}
trap on_failure ERR

# ---- Prerequisites ----
step "Checking this Mac"
command -v docker >/dev/null 2>&1 \
  || stop_with "Docker is not installed. Install Docker Desktop from docker.com, open it, then run this again."
docker info >/dev/null 2>&1 \
  || stop_with "Docker is not running. Open Docker Desktop, wait until it says it is running, then run this again."
docker compose version >/dev/null 2>&1 \
  || stop_with "Docker Compose is missing. Update Docker Desktop, then run this again."

if [ -n "$(compose ps -aq 2>/dev/null)" ]; then
  echo "A previous demo is still here, so it will be reset first."
  compose down -v --remove-orphans >"$LOG_DIR/reset.log" 2>&1
fi

for port in 80 3000 5050 8080; do
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    user="$(lsof -nP -iTCP:"$port" -sTCP:LISTEN | awk 'NR==2 {print $1}')"
    stop_with "Port $port is in use by $user. Quit that app or stop that server, then run this again."
  fi
done

memory_gb=$(($(docker info --format '{{.MemTotal}}') / 1073741824))
if [ "$memory_gb" -lt 6 ]; then
  echo "Warning: Docker has about ${memory_gb} GB of memory. Building needs about 6 GB. If it fails, raise it in Docker Desktop, Settings, Resources."
fi

# ---- Build ----
current_step="building the API and website"
step "Building the API and website (about 10 minutes the first time, a few minutes after code changes)"
# The website's Dockerfile with the demo's build-time switches added before its build step.
awk '{ print } /^ENV NEXT_PUBLIC_GA_MEASUREMENT_ID=/ {
  print "ARG NEXT_PUBLIC_COURSE_QUALITY_DEMO"; print "ENV NEXT_PUBLIC_COURSE_QUALITY_DEMO=${NEXT_PUBLIC_COURSE_QUALITY_DEMO}"
  print "ARG NEXT_PUBLIC_COURSE_QUALITY_DEMO_ORG_ID"; print "ENV NEXT_PUBLIC_COURSE_QUALITY_DEMO_ORG_ID=${NEXT_PUBLIC_COURSE_QUALITY_DEMO_ORG_ID}" }' \
  "$REPO_ROOT/src/web/Dockerfile" >"$CACHE_DIR/web.Dockerfile"
grep -q NEXT_PUBLIC_COURSE_QUALITY_DEMO_ORG_ID "$CACHE_DIR/web.Dockerfile" \
  || stop_with "src/web/Dockerfile changed shape, so the demo could not add its build settings. See README.md, Troubleshooting."
compose build api web >"$LOG_DIR/build.log" 2>&1

# ---- Database ----
if [ "$FROM_SNAPSHOT" = true ]; then
  # shellcheck disable=SC1090
  source "$SNAPSHOT_INFO"
  current_step="loading the saved demo database"
  step "Loading the saved demo database from $SNAPSHOT_SAVED ($SNAPSHOT_COURSES courses)"
  compose up -d postgres >"$LOG_DIR/up.log" 2>&1
  # Over TCP: on first boot the image runs a temporary server on its socket only, then restarts.
  until compose exec -T postgres pg_isready -h 127.0.0.1 -U postgres >/dev/null 2>&1; do sleep 1; done
  compose exec -T postgres createdb -U postgres yoma-dev
  compose exec -T postgres pg_restore -U postgres -d yoma-dev --no-owner <"$SNAPSHOT" >"$LOG_DIR/restore.log" 2>&1 || true
  [ "$(sql -At -c 'SELECT count(*) FROM "Opportunity"."Opportunity"')" = "$SNAPSHOT_COURSES" ] \
    || stop_with "The saved database did not load. Run ./start.sh --refresh to build it again."
  # Accounts link to this start's new Keycloak users when they first sign in.
  sql -c 'UPDATE "Entity"."User" SET "ExternalId" = NULL'
  sql -c 'DROP SCHEMA IF EXISTS "HangFire" CASCADE'
fi

current_step="starting the services"
step "Starting Postgres, Valkey, Keycloak and the API (the database migrations run here)"
compose up -d api fixtures runner >>"$LOG_DIR/up.log" 2>&1
wait_for_url "http://localhost:5050/api/v3/health/live" "The API" 300
until [ "$(sql -At -c "SELECT to_regclass('\"CourseQuality\".\"Assessment\"') IS NOT NULL" 2>/dev/null)" = "t" ]; do sleep 2; done

current_step="creating the local test accounts"
step "Creating the local test accounts"
compose run --rm --no-deps keycloak-users >"$LOG_DIR/users.log" 2>&1
compose run --rm --no-deps -v "$DEMO_DIR/seed:/seed:ro" --entrypoint /bin/sh keycloak-users /seed/admin-partner-role.sh >>"$LOG_DIR/users.log" 2>&1

seed_args=()
[ "$USE_FIXTURES" = true ] && seed_args+=(--fixtures)
live_summary=""

if [ "$FROM_SNAPSHOT" = true ]; then
  current_step="updating the named courses"
  step "Setting the named courses 101 to 108 to today's dates"
  in_runner node /demo/seed/generate-sql.mjs --named-only ${seed_args[@]+"${seed_args[@]}"} | sql >"$LOG_DIR/seed.log" 2>&1
else
  if [ ! -f "$CACHE_DIR/live-catalogue.json" ]; then
    current_step="downloading the live course list"
    step "Downloading the public list of live Yoma courses (about a minute)"
    in_runner node /demo/seed/fetch-live-catalogue.mjs 2>"$LOG_DIR/fetch.log"
  fi
  current_step="adding the courses"
  step "Adding every live Yoma course, the named courses and the scaled demo participation"
  in_runner node /demo/seed/generate-sql.mjs ${seed_args[@]+"${seed_args[@]}"} | sql >"$LOG_DIR/seed.log" 2>&1
  rm -f "$CACHE_DIR/assessment-progress.jsonl"

  if [ "$(sql -At -c "SELECT to_regclass('\"CourseQuality\".\"LearnerFeedback\"') IS NOT NULL")" = "t" ]; then
    step "Adding the synthetic learner feedback"
    in_runner node /demo/seed/generate-feedback-sql.mjs 2>>"$LOG_DIR/seed.log" | sql >>"$LOG_DIR/seed.log" 2>&1
  else
    echo "This API has no learner feedback table yet, so no synthetic feedback was added."
  fi

fi

# Live jobs, listed only, for the My Yoma pathways' job step. Nothing is invented about them. Also after loading a saved
# database from before jobs were added; skipped when it already has them.
if [ "$(sql -At -c "SELECT count(*) FROM \"Opportunity\".\"Opportunity\" o JOIN \"Opportunity\".\"OpportunityType\" t ON t.\"Id\" = o.\"TypeId\" AND t.\"Name\" = 'Job'")" = "0" ]; then
  if [ ! -f "$CACHE_DIR/live-jobs.json" ]; then
    step "Downloading up to 2,000 live Yoma jobs (about a minute)"
    in_runner node /demo/seed/fetch-live-catalogue.mjs --jobs 2>>"$LOG_DIR/fetch.log" || echo "The live jobs did not download, so pathways show no jobs."
  fi
  step "Adding the live jobs"
  in_runner node /demo/seed/generate-jobs-sql.mjs 2>>"$LOG_DIR/seed.log" | sql >>"$LOG_DIR/seed.log" 2>&1
fi

# ---- Assess ----
current_step="running the quality assessment"
step "Running the quality assessment on the named courses"
results="$LOG_DIR/assessment.jsonl"
in_runner node /demo/seed/assess.mjs >"$results"

# A real page can change or be down, so a course that misses its demo state moves to its demo page.
fallback_ids=""
if [ "$USE_FIXTURES" = false ]; then
  while IFS= read -r line; do
    id="$(sed -E 's/.*"id":"([^"]+)".*/\1/' <<<"$line")"
    url="$(sed -E 's/.*"fixtureUrl":"([^"]+)".*/\1/' <<<"$line")"
    echo "$id: the real page did not give the expected result, using its demo page"
    sql -c "UPDATE \"Opportunity\".\"Opportunity\" SET \"URL\" = '$url', \"DateModified\" = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC') WHERE \"Id\" = '$id'" </dev/null
    fallback_ids="$fallback_ids $id"
  done < <(grep '"ok":false' "$results" | grep '"hasLive":true' || true)

  if [ -n "$fallback_ids" ]; then
    ids="$(echo $fallback_ids | tr ' ' ',')"
    in_runner node /demo/seed/assess.mjs --ids "$ids" >>"$results"
  fi
fi

all_as_expected=0
results_table="$(in_runner env FALLBACK_IDS="$fallback_ids" USE_FIXTURES="$USE_FIXTURES" node /demo/seed/summary.mjs <"$results")" || all_as_expected=$?

live_ok=0
if [ "$FROM_SNAPSHOT" = true ]; then
  step "Grading the catalogue again, now that the named courses are reassessed"
  live_summary="$(in_runner node /demo/seed/grade.mjs)"
elif [ -n "$SAMPLE" ]; then
  step "Assessing a sample of $SAMPLE live courses from mixed hosts, then grading"
  live_summary="$(in_runner node /demo/seed/assess-live.mjs --sample "$SAMPLE" 2>"$LOG_DIR/assess-live.log")" || live_ok=$?
else
  live_count="$(sql -At -c "SELECT count(*) FROM \"Opportunity\".\"Opportunity\" o JOIN \"Opportunity\".\"OpportunityType\" t ON t.\"Id\" = o.\"TypeId\" AND t.\"Name\" = 'Learning' WHERE o.\"Id\"::text NOT LIKE 'c0a7e000-%'")"
  step "Assessing all $live_count live courses at the product's pace, then grading (a few hours; progress is in .logs/assess-live.log)"
  live_summary="$(in_runner node /demo/seed/assess-live.mjs 2>"$LOG_DIR/assess-live.log")" || live_ok=$?
  if [ "$live_ok" -eq 0 ]; then
    current_step="saving the demo database"
    step "Saving the demo database, so the next start takes minutes"
    save_snapshot
  else
    echo "Some courses could not be assessed, so the database was not saved. Run ./assess-all.sh to retry them and save it."
  fi
fi

# ---- Website ----
# Started only now, so no one can open the course list, and cache it, before the courses are in.
current_step="starting the website"
step "Starting the website and loading the course list once"
compose up -d web >>"$LOG_DIR/up.log" 2>&1
wait_for_url "http://localhost:3000/" "The website" 180
warm_browse

# ---- Done ----
trap - ERR
web="http://localhost:3000"
courses_total="$(sql -At -c 'SELECT count(*) FROM "Opportunity"."Opportunity"')"
assessed_total="$(sql -At -c 'SELECT count(*) FROM "CourseQuality"."Assessment" WHERE "IsLatest"')"

printf '\n=========================================================\n'
printf ' Course Quality demo is running (took %dm %02ds)\n' $((SECONDS / 60)) $((SECONDS % 60))
printf '=========================================================\n\n'
printf '  Learner, course list:  %s/opportunities\n' "$web"
printf '  Reviewer queue:        %s/admin/course-quality\n' "$web"
printf '  Partner fix notes:     %s/organisations/%s/course-quality\n' "$web" "$ORG_ID"
printf '  Named learner cards:   %s/opportunities/c0a7e000-0000-4000-8000-000000000101  (101 to 108)\n' "$web"
printf '\nLocal test sign-ins, the public seed defaults from src/api/docker-compose.yml:\n'
printf '  Reviewer and partner: testadminuser@gmail.com  (one account for all three views)\n'
printf '  Partner only:         testorgadminuser@gmail.com\n'
printf '  Learner:              testuser@gmail.com\n'
printf '  Password:             P@ssword12\n'
printf '\n%s courses, %s assessed.\n' "$courses_total" "$assessed_total"
[ -n "$live_summary" ] && printf 'Live courses:\n%s\n' "$live_summary"
printf '\nNamed courses:\n%s\n' "$results_table"
if [ "$all_as_expected" -ne 0 ]; then
  printf '\nSome named courses did not reach their demo state. Check them before presenting.\n'
fi
printf '\nWalkthrough: %s/README.md\n' "${DEMO_DIR#"$REPO_ROOT"/}"
printf 'When you finish: %s/stop.sh\n' "${DEMO_DIR#"$REPO_ROOT"/}"
