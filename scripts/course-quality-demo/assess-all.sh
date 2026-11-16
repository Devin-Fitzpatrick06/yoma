#!/bin/bash
# Assesses every live course in the RUNNING demo that is not assessed yet, grades the catalogue, and saves the demo
# database so later starts load it in minutes. Resumes where a stopped run left off. See README.md.
#   ./assess-all.sh                  the rest of the catalogue
#   ./assess-all.sh --concurrency 6  hosts in parallel (default 4); each host still gets one request at a time
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

[ -n "$(compose ps -q runner 2>/dev/null)" ] || { echo "The demo is not running. Start it with ./start.sh first."; exit 1; }
DEMO_BASE_DATE="$(compose exec -T runner printenv DEMO_BASE_DATE)"

step "Assessing the live courses not yet assessed, then grading (progress is in .logs/assess-live.log)"
status=0
in_runner node /demo/seed/assess-live.mjs "$@" 2>>"$LOG_DIR/assess-live.log" || status=$?

if [ "$status" -eq 0 ]; then
  step "Saving the demo database"
  save_snapshot
else
  echo "Some courses could not be assessed, so the database was not saved. Run ./assess-all.sh again to retry them."
fi
printf '\nThe course list refreshes within 5 minutes.\n'
exit "$status"
