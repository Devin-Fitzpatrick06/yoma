#!/bin/bash
# Shared by start.sh and assess-all.sh. Not run on its own.

DEMO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$DEMO_DIR/../.." && pwd)"
LOG_DIR="$DEMO_DIR/.logs"
CACHE_DIR="$DEMO_DIR/.cache"
SNAPSHOT="$CACHE_DIR/demo-db.pgdump"
SNAPSHOT_INFO="$CACHE_DIR/demo-db.env"
ORG_ID="c0a7e000-0000-4000-8000-000000000001"
mkdir -p "$LOG_DIR" "$CACHE_DIR"

compose() { docker compose -f "$DEMO_DIR/docker-compose.demo.yml" --profile setup "$@"; }
sql() { compose exec -T postgres psql -q -U postgres -d yoma-dev "$@"; }
in_runner() { compose exec -T runner "$@"; }
step() { printf '\n[%3dm%02ds] %s\n' $((SECONDS / 60)) $((SECONDS % 60)) "$1"; }

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

# Saves the whole demo database, compressed, so a later start loads it in seconds. Hangfire's job store is left out:
# the API creates it again on start.
save_snapshot() {
  compose exec -T postgres pg_dump -U postgres -d yoma-dev -Fc -Z 6 --exclude-schema='"HangFire"' >"$SNAPSHOT.partial"
  mv "$SNAPSHOT.partial" "$SNAPSHOT"
  {
    echo "SNAPSHOT_BASE_DATE=$DEMO_BASE_DATE"
    echo "SNAPSHOT_SAVED=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo "SNAPSHOT_COURSES=$(sql -At -c 'SELECT count(*) FROM "Opportunity"."Opportunity"')"
    echo "SNAPSHOT_ASSESSED=$(sql -At -c 'SELECT count(*) FROM "CourseQuality"."Assessment" WHERE "IsLatest"')"
  } >"$SNAPSHOT_INFO"
  echo "Saved the demo database to ${SNAPSHOT#"$REPO_ROOT"/} ($(du -h "$SNAPSHOT" | cut -f1 | tr -d ' '))."
}

# The browse page is cached for 5 minutes from its first request, so it is requested once here, after the courses are in,
# and the first page a person sees already lists them.
warm_browse() {
  local tries=0
  until curl -fsS --max-time 60 "http://localhost:3000/opportunities" 2>/dev/null | grep -q '"organizationName"'; do
    tries=$((tries + 1))
    if [ "$tries" -ge 10 ]; then
      echo "The course list did not show courses after 10 tries." >&2
      return 1
    fi
    sleep 3
  done
}
