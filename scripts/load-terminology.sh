#!/usr/bin/env bash
#
# Push Prohori's terminology resources (CodeSystem/ValueSet/ConceptMap, Phase K)
# into a FHIR server so its $expand / $validate-code / $translate operations
# can resolve them. Run after `docker compose -f deploy/docker-compose.yml up hapi`
# and `(cd ig && sushi . --snapshot)`.
#
# Usage:  bash scripts/load-terminology.sh [BASE_URL]
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
for f in "$GEN"/CodeSystem-*.json "$GEN"/ValueSet-*.json "$GEN"/ConceptMap-*.json; do
  type=$(jq -r '.resourceType' "$f")
  id=$(jq -r '.id' "$f")
  code=$(curl -sS -o /tmp/prohori-load-tx.json -w '%{http_code}' -X PUT "$BASE/$type/$id" \
    -H 'Content-Type: application/fhir+json' --data-binary @"$f")
  echo "PUT $type/$id -> $code"
  if [ "$code" -ge 400 ]; then cat /tmp/prohori-load-tx.json; exit 1; fi
done

echo
echo "Server can now resolve \$expand / \$validate-code / \$translate against these."
