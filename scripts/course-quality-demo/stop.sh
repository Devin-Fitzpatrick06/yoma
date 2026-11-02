#!/bin/bash
# Stops the demo and removes its containers, network and data. Nothing else is touched.
set -euo pipefail

DEMO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not running, so there is nothing to stop."
  exit 0
fi

docker compose -f "$DEMO_DIR/docker-compose.demo.yml" --profile setup down -v --remove-orphans

left="$(docker ps -aq --filter label=com.docker.compose.project=yoma-course-quality-demo | wc -l | tr -d ' ')"
if [ "$left" -ne 0 ]; then
  echo "Warning: $left demo containers are still here. Run: docker ps -a"
  exit 1
fi

echo "The demo has stopped. Its containers and data are gone. The built images stay, so the next start is quicker."
