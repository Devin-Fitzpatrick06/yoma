#!/bin/bash
# Starts the three other prototypes of the connected journey on this machine, from local clones. See README.md.
#   PATHFINDER_DIR=... SKILLLENS_DIR=... YOCV_DIR=... scripts/connected-journey/journey.sh   start all three
#   scripts/connected-journey/journey.sh stop                                                stop what this script started
#   scripts/connected-journey/journey.sh status                                              which are running
#   scripts/connected-journey/journey.sh reset                                               un-save Halima's solar job, to rerun the demo
# Ziqi (Phase 1) is not started here: that is scripts/course-quality-demo/start.sh --journey.
# Each clone needs its connected-journey patch first (docs/work/active/course-quality-judger/connected-journey/patches/).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="$HERE/.logs"
mkdir -p "$LOG_DIR"

# The journey contract's local addresses (journey-contract.md, "Addresses").
ZIQI_URL="${ZIQI_URL:-http://localhost:3000}"
PATHFINDER_PORT=4173
SKILLLENS_PORT=3001
YOCV_PORT=4174
LISTING_ID="c0a7e000-0000-4000-8000-000000000301" # the Yoma listing for the shared opp-job-001
LISTING="$ZIQI_URL/opportunities/$LISTING_ID"
HALIMA_EMAIL="halima.yusuf@example.org"
# The Ziqi demo's database container (scripts/course-quality-demo/docker-compose.demo.yml, project yoma-course-quality-demo).
DEMO_POSTGRES="${DEMO_POSTGRES:-yoma-course-quality-demo-postgres-1}"

running() { [ -f "$LOG_DIR/$1.pid" ] && kill -0 "$(cat "$LOG_DIR/$1.pid")" 2>/dev/null; }
port_busy() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }

# A process and everything it started (npm starts next, which starts its own workers), children first.
kill_tree() {
  local child
  for child in $(pgrep -P "$1" 2>/dev/null); do kill_tree "$child"; done
  kill -TERM "$1" 2>/dev/null || true
}

stop_all() {
  for app in pathfinder skilllens yocv; do
    if running "$app"; then
      kill_tree "$(cat "$LOG_DIR/$app.pid")"
      echo "Stopped $app."
    fi
    rm -f "$LOG_DIR/$app.pid"
  done
}

status_all() {
  for app in pathfinder skilllens yocv; do
    if running "$app"; then echo "$app: running (pid $(cat "$LOG_DIR/$app.pid"), log .logs/$app.log)"; else echo "$app: not running"; fi
  done
}

# Halima's save of the solar job, removed so the demo (or a Playwright run) can start again from "Save". The same DELETE
# as scripts/course-quality-demo/seed/generate-journey-sql.mjs, which start.sh --journey runs on every start.
reset_journey() {
  command -v docker >/dev/null 2>&1 || { echo "Docker is not installed, so the Ziqi demo database cannot be reached."; exit 2; }
  docker inspect "$DEMO_POSTGRES" >/dev/null 2>&1 \
    || { echo "The Ziqi demo database ($DEMO_POSTGRES) is not running. Start it with scripts/course-quality-demo/start.sh --journey."; exit 2; }
  local removed
  removed="$(docker exec -i "$DEMO_POSTGRES" psql -q -At -U postgres -d yoma-dev <<SQL
WITH removed AS (
  DELETE FROM "Opportunity"."MyOpportunity"
  WHERE "UserId" = (SELECT "Id" FROM "Entity"."User" WHERE "Email" = '$HALIMA_EMAIL')
    AND "OpportunityId" = '$LISTING_ID'
    AND "ActionId" = (SELECT "Id" FROM "Opportunity"."MyOpportunityAction" WHERE "Name" = 'Saved')
  RETURNING 1)
SELECT count(*) FROM removed;
SQL
)"
  if [ "$removed" = "0" ]; then echo "Halima had not saved the solar job: nothing to reset."; else echo "Halima's save of the solar job is removed."; fi
  echo "Reload the listing in the browser: it shows Save again."
}

case "${1:-start}" in
  stop) stop_all; exit 0 ;;
  status) status_all; exit 0 ;;
  reset) reset_journey; exit 0 ;;
  start) ;;
  *) echo "Unknown command: $1. Use start (the default), stop, status or reset."; exit 2 ;;
esac

# ---- Checks ----
need_dir() { # name, variable, value
  [ -n "$3" ] || { echo "Set $2 to your local clone of $1, for example $2=~/code/$1."; exit 2; }
  [ -f "$3/package.json" ] || { echo "$2=$3 is not a clone of $1 (no package.json there)."; exit 2; }
}
need_dir pathfinder-prototype PATHFINDER_DIR "${PATHFINDER_DIR:-}"
need_dir skilllens SKILLLENS_DIR "${SKILLLENS_DIR:-}"
need_dir yocv-prototype YOCV_DIR "${YOCV_DIR:-}"
command -v node >/dev/null 2>&1 || { echo "Node is not installed. SkillLens needs Node 24."; exit 2; }

# Without its patch an app runs, but shows no journey cards and links nowhere. Warn, do not stop.
[ -f "$PATHFINDER_DIR/engine/journey.js" ] \
  || echo "Warning: PathFinder has no engine/journey.js, so its connected-journey patch is not applied. See README.md, Patches."
[ -f "$SKILLLENS_DIR/lib/journey.ts" ] \
  || echo "Warning: SkillLens has no lib/journey.ts, so its connected-journey patch is not applied. See README.md, Patches."
grep -q "journeyBackHref" "$YOCV_DIR/core.js" 2>/dev/null \
  || echo "Warning: YoCV has no journeyBackHref in core.js, so its connected-journey patch is not applied. See README.md, Patches."

for app in pathfinder skilllens yocv; do
  if running "$app"; then echo "$app is already running from this script. Run: $0 stop"; exit 1; fi
done
for port in $PATHFINDER_PORT $SKILLLENS_PORT $YOCV_PORT; do
  if port_busy "$port"; then echo "Port $port is in use. Stop whatever uses it, then run this again."; exit 1; fi
done

# ---- Start ----
start_app() { # name, directory, command...
  local name="$1" dir="$2"
  shift 2
  (cd "$dir" || exit 1; nohup "$@" >"$LOG_DIR/$name.log" 2>&1 </dev/null & echo $! >"$LOG_DIR/$name.pid")
  echo "Started $name (log: scripts/connected-journey/.logs/$name.log)."
}

if [ ! -d "$SKILLLENS_DIR/node_modules" ]; then
  echo "Installing SkillLens's packages once (npm ci, a few minutes)..."
  (cd "$SKILLLENS_DIR" && npm ci) >"$LOG_DIR/skilllens-install.log" 2>&1
fi
if [ ! -f "$SKILLLENS_DIR/.env.local" ]; then
  # SkillLens's own setup: simulated AI unless Ollama has its model, and a signing secret, written to its .env.local only.
  (cd "$SKILLLENS_DIR" && npm run setup) >>"$LOG_DIR/skilllens-install.log" 2>&1
fi

# SkillLens deterministic and offline: the mock AI, the stub content-safety check and the opportunities snapshot, unless
# already set in this shell. The pass signing secret is made once and kept in .logs/ (git-ignored, never printed), so a
# restart keeps the passes it signed valid.
# Passed to SkillLens only (the other two apps do not get them).
SKILLLENS_AI_PROVIDER="${AI_PROVIDER:-mock}"
SKILLLENS_CONTENT_SAFETY_PROVIDER="${CONTENT_SAFETY_PROVIDER:-stub}"
SKILLLENS_YOMA_OPPORTUNITIES="${YOMA_OPPORTUNITIES:-snapshot}"
SKILLLENS_PASS_SIGNING_SECRET="${PASS_SIGNING_SECRET:-}"
if [ -z "$SKILLLENS_PASS_SIGNING_SECRET" ]; then
  SECRET_FILE="$LOG_DIR/skilllens-pass-signing-secret"
  if [ ! -s "$SECRET_FILE" ]; then
    (umask 077 && node -e 'process.stdout.write(require("crypto").randomBytes(32).toString("hex"))' >"$SECRET_FILE")
  fi
  SKILLLENS_PASS_SIGNING_SECRET="$(cat "$SECRET_FILE")"
fi

start_app pathfinder "$PATHFINDER_DIR" env PORT="$PATHFINDER_PORT" NEXT_PROTOTYPE_URL="http://localhost:$SKILLLENS_PORT/demo/p2e" node serve.mjs
start_app skilllens "$SKILLLENS_DIR" env NEXT_PUBLIC_YOCV_URL="http://localhost:$YOCV_PORT" \
  AI_PROVIDER="$SKILLLENS_AI_PROVIDER" CONTENT_SAFETY_PROVIDER="$SKILLLENS_CONTENT_SAFETY_PROVIDER" \
  YOMA_OPPORTUNITIES="$SKILLLENS_YOMA_OPPORTUNITIES" PASS_SIGNING_SECRET="$SKILLLENS_PASS_SIGNING_SECRET" \
  npm run dev -- -p "$SKILLLENS_PORT"
start_app yocv "$YOCV_DIR" env PORT="$YOCV_PORT" node server.js

printf 'Waiting for the three apps'
for _ in $(seq 1 60); do
  up=0
  for port in $PATHFINDER_PORT $SKILLLENS_PORT $YOCV_PORT; do port_busy "$port" && up=$((up + 1)); done
  [ "$up" -eq 3 ] && break
  printf '.'
  sleep 2
done
printf '\n'
status_all

cat <<EOF

Connected journey (fictional shared mock data v1.1; every screen says mock-up or sample data)

  Ziqi        $ZIQI_URL           (scripts/course-quality-demo/start.sh --journey)
  PathFinder  http://localhost:$PATHFINDER_PORT/
  SkillLens   http://localhost:$SKILLLENS_PORT/demo/p2e  (the first page takes a few seconds while Next builds it)
  YoCV        http://localhost:$YOCV_PORT/

The click path, all in one tab:
  1. Ziqi: sign in as halima.yusuf@example.org / P@ssword12. My Yoma asks 4 quick questions the first time (pick
     "Sales & customer service"), then shows her route on Yoma. Open the solar job ($LISTING):
     it shows "Checked by Ziqi: Verified · Grade A". Save it.
  2. "Plan it with PathFinder" opens PathFinder: the plan, with the scam job held back from it.
  3. "Continue to SkillLens" opens SkillLens: switch to Computer view if it opens in Phone view. The challenge, passed
     on attempt 2. Tap "Save to my profile", then "Add Skill Credential to my YoID".
  4. "Continue to YoCV" opens YoCV: pick the role "Solar installation assistant" if it is not preselected. The CV with
     the verified credential, the QR, Apply.
  5. "Back to the listing" returns to the Ziqi listing, which shows "Job Application received" with the thread complete.

Run it again: scripts/connected-journey/journey.sh reset  (un-saves Halima's solar job)
Stop the three apps with: scripts/connected-journey/journey.sh stop
EOF
