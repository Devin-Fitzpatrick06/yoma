#!/bin/bash
# Lists the courses Yoma's current review approves (publicly visible) but Zeki holds back, with counts by failure type.
# Read-only. See docs/work/active/course-quality-judger/zeki-vs-current-review.md.
#   ./compare-review.sh                         against the RUNNING demo database
#   ./compare-review.sh --list 200              show 200 courses in the list (default 50)
#   ./compare-review.sh --csv > held-back.csv   every held-back course, as CSV
#   ./compare-review.sh --database-url URL      against another database with the same schema, through a local psql
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LIST=50
CSV=0
URL=""

while [ $# -gt 0 ]; do
  case "$1" in
    --list) LIST="${2:?--list needs a number}"; shift 2 ;;
    --csv) CSV=1; shift ;;
    --database-url) URL="${2:?--database-url needs a URL}"; shift 2 ;;
    -h|--help) sed -n '2,8p' "$0"; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

[[ "$LIST" =~ ^[0-9]+$ ]] || { echo "--list needs a number" >&2; exit 2; }

if [ -n "$URL" ]; then
  psql "$URL" -q -X -v list_limit="$LIST" -v csv="$CSV" -f "$DIR/compare-review.sql"
else
  source "$DIR/lib.sh"
  [ -n "$(compose ps -q postgres 2>/dev/null)" ] || { echo "The demo is not running. Start it with ./start.sh first, or pass --database-url." >&2; exit 1; }
  sql -X -v list_limit="$LIST" -v csv="$CSV" <"$DIR/compare-review.sql"
fi
