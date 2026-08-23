#!/usr/bin/env bash
set -Eeuo pipefail

# Deploys one immutable MM image. It deliberately does not edit .env or perform
# git conflict recovery because those actions require an operator decision.
repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_dir"

if [[ ! -f .env ]]; then
  echo "Missing .env. Copy .env.example and replace every placeholder." >&2
  exit 1
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker is not accessible. Re-login after joining the docker group, or use sudo for this session." >&2
  exit 1
fi

if [[ $# -gt 0 ]]; then
  export MM_VERSION="$1"
fi

docker compose config --quiet
docker compose pull
docker compose up -d --remove-orphans
docker compose ps

