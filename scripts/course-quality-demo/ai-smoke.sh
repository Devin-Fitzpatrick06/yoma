#!/bin/bash
# One-command proof of "a live LLM through the Yoma backend, with tokens and cost": with the demo running and its AI on
# (start.sh --local-ai, --local-ai-url or --ai-url), runs Ziqi's assessment on one named course through the API and
# prints the AI's model, tokens, cost, duration and outcome. See README.md, "Ziqi AI".
#   ./ai-smoke.sh          named course 102 (its demo page is always there)
#   ./ai-smoke.sh 105      another named course, 101 to 108
#   ./ai-smoke.sh <id>     any opportunity id in the demo
# The AI can only lower a result, so the course may move to "Needs a look" afterwards; ./start.sh resets it.
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

[ -n "$(compose ps -q runner 2>/dev/null)" ] || { echo "The demo is not running. Start it with ./start.sh --local-ai first."; exit 1; }
provider="$(compose exec -T api printenv AI__Provider 2>/dev/null || true)"
if [ "$provider" != OpenAICompatible ]; then
  echo "Ziqi's AI is off in the running demo (AI__Provider is ${provider:-not set}). Restart it with ./start.sh --local-ai."
  exit 1
fi
printf 'AI:       %s, model %s at %s\n' "$provider" \
  "$(compose exec -T api printenv AI__OpenAICompatible__Model || true)" "$(compose exec -T api printenv AI__OpenAICompatible__BaseUrl || true)"

in_runner node /demo/seed/ai-smoke.mjs "${1:-102}"
