#!/usr/bin/env bash
#
# Push the generated Prohori StructureDefinition(s) into a FHIR server so it can
# enforce them on writes. Run after `docker compose -f deploy/docker-compose.yml up`.
#
# Usage:  bash scripts/load-profile.sh [BASE_URL]
# Default BASE_URL: http://localhost:8080/fhir
#
set -euo pipefail
BASE="${1:-http://localhost:8080/fhir}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GEN="$DIR/ig/fsh-generated/resources"

if [ ! -d "$GEN" ]; then
  echo "No generated resources at $GEN — run:  (cd ig && sushi . --snapshot)" >&2
  exit 1
fi

shopt -s nullglob
for sd in "$GEN"/StructureDefinition-*.json; do
  id=$(jq -r '.id' "$sd")
  code=$(curl -sS -o /dev/null -w '%{http_code}' -X PUT "$BASE/StructureDefinition/$id" \
    -H 'Content-Type: application/fhir+json' --data-binary @"$sd")
  echo "PUT StructureDefinition/$id -> $code"
done

echo
echo "Server will now validate writes whose meta.profile points at a loaded profile."
