#!/bin/bash
# Starts the whole Course Quality demo with one command. See README.md.
#   ./start.sh              use real course pages where possible, demo pages otherwise
#   ./start.sh --fixtures   use only the demo pages (works with no internet once images are built)
set -euo pipefail

DEMO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$DEMO_DIR/../.." && pwd)"
LOG_DIR="$DEMO_DIR/.logs"
ORG_ID="c0a7e000-0000-4000-8000-000000000001"
USE_FIXTURES=false

for arg in "$@"; do
  case "$arg" in
    --fixtures) USE_FIXTURES=true ;;
    *) echo "Unknown option: $arg. The only option is --fixtures."; exit 2 ;;
  esac
done

export DEMO_BASE_DATE
DEMO_BASE_DATE="$(date -u +%Y-%m-%d)"
export DEMO_NEXTAUTH_SECRET
DEMO_NEXTAUTH_SECRET="$(openssl rand -hex 32)"

compose() { docker compose -f "$DEMO_DIR/docker-compose.demo.yml" --profile setup "$@"; }
sql() { compose exec -T postgres psql -q -U postgres -d yoma-dev "$@"; }
in_web() { compose exec -T web "$@"; }
step() { printf '\n[%3dm%02ds] %s\n' $((SECONDS / 60)) $((SECONDS % 60)) "$1"; }
stop_with() { printf '\nCould not start the demo.\n%s\n' "$1" >&2; exit 1; }

current_step="checking this Mac"
on_failure() {
  printf '\nThe demo stopped while %s.\n' "$current_step" >&2
  printf 'Details are in %s\n' "$LOG_DIR" >&2
  printf 'Run ./stop.sh to clean up, then try ./start.sh again.\n' >&2
}
trap on_failure ERR

wait_for_url() { # url, what, seconds
  local waited=0
  until curl -fsS -o /dev/null --max-time 5 "$1" 2>/dev/null; do
    waited=$((waited + 3))
    if [ "$waited" -ge "$3" ]; then
      echo "$2 did not become ready within $3 seconds." >&2
      return 1
    fi
    sleep 3
  done
}

# ---- Prerequisites ----
step "Checking this Mac"
command -v docker >/dev/null 2>&1 \
  || stop_with "Docker is not installed. Install Docker Desktop from docker.com, open it, then run this again."
docker info >/dev/null 2>&1 \
  || stop_with "Docker is not running. Open Docker Desktop, wait until it says it is running, then run this again."
docker compose version >/dev/null 2>&1 \
  || stop_with "Docker Compose is missing. Update Docker Desktop, then run this again."

mkdir -p "$LOG_DIR"

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

# ---- Build and start ----
current_step="building the API and website"
step "Building the API and website (about 10 minutes the first time, under a minute after that)"
compose build api web >"$LOG_DIR/build.log" 2>&1

current_step="starting the services"
step "Starting Postgres, Valkey, Keycloak, the API and the website (the database migrations run here)"
compose up -d web fixtures >"$LOG_DIR/up.log" 2>&1

wait_for_url "http://localhost:5050/api/v3/health/live" "The API" 300
wait_for_url "http://localhost:3000/" "The website" 120

until [ "$(sql -At -c "SELECT to_regclass('\"CourseQuality\".\"Assessment\"') IS NOT NULL" 2>/dev/null)" = "t" ]; do sleep 2; done

# ---- Seed ----
current_step="creating the local test accounts"
step "Creating the local test accounts"
compose run --rm --no-deps keycloak-users >"$LOG_DIR/users.log" 2>&1

current_step="adding the demo courses"
step "Adding the demo courses"
seed_args=()
[ "$USE_FIXTURES" = true ] && seed_args+=(--fixtures)
in_web node /demo/seed/generate-sql.mjs ${seed_args[@]+"${seed_args[@]}"} | sql >"$LOG_DIR/seed.log" 2>&1

# ---- Assess ----
current_step="running the quality assessment"
step "Running the quality assessment on the demo courses"
results="$LOG_DIR/assessment.jsonl"
in_web node /demo/seed/assess.mjs >"$results"

# A real page can change or be down, so a course that misses its demo state moves to its demo page.
fallback_ids=""
if [ "$USE_FIXTURES" = false ]; then
  while IFS= read -r line; do
    id="$(sed -E 's/.*"id":"([^"]+)".*/\1/' <<<"$line")"
    url="$(sed -E 's/.*"fixtureUrl":"([^"]+)".*/\1/' <<<"$line")"
    echo "$id: the real page did not give the expected result, using its demo page"
    sql -c "UPDATE \"Opportunity\".\"Opportunity\" SET \"URL\" = '$url', \"DateModified\" = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC') WHERE \"Id\" = '$id'"
    fallback_ids="$fallback_ids $id"
  done < <(grep '"ok":false' "$results" | grep '"hasLive":true' || true)

  if [ -n "$fallback_ids" ]; then
    ids="$(echo $fallback_ids | tr ' ' ',')"
    in_web node /demo/seed/assess.mjs --ids "$ids" >>"$results"
  fi
fi

all_as_expected=0
results_table="$(in_web env FALLBACK_IDS="$fallback_ids" USE_FIXTURES="$USE_FIXTURES" node /demo/seed/summary.mjs <"$results")" || all_as_expected=$?

# ---- Done ----
trap - ERR
web="http://localhost:3000"

printf '\n=========================================================\n'
printf ' Course Quality demo is running (took %dm %02ds)\n' $((SECONDS / 60)) $((SECONDS % 60))
printf '=========================================================\n\n'
printf '  Course list:      %s/opportunities\n' "$web"
printf '  Learner cards:    %s/opportunities/c0a7e000-0000-4000-8000-000000000101  (the named courses end 101 to 108)\n' "$web"
printf '  Reviewer queue:   %s/admin/course-quality\n' "$web"
printf '  Partner fix notes: %s/organisations/%s/course-quality\n' "$web" "$ORG_ID"
printf '\nLocal test sign-ins, the public seed defaults from src/api/docker-compose.yml:\n'
printf '  Reviewer:        testadminuser@gmail.com\n'
printf '  Partner:         testorgadminuser@gmail.com\n'
printf '  Learner:         testuser@gmail.com\n'
printf '  Password:        P@ssword12\n'
printf '\nDemo courses:\n%s\n' "$results_table"
if [ "$all_as_expected" -ne 0 ]; then
  printf '\nSome courses did not reach their demo state. Check them before presenting.\n'
fi
printf '\nWalkthrough: %s/README.md\n' "${DEMO_DIR#"$REPO_ROOT"/}"
printf 'When you finish: %s/stop.sh\n' "${DEMO_DIR#"$REPO_ROOT"/}"
