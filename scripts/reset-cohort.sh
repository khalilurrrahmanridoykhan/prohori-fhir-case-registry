#!/usr/bin/env bash
#
# Delete every resource tagged urn:prohori|demo-cohort from a FHIR server, so you
# can re-seed a clean set. Order matters: Condition/Observation/Encounter before
# Patient (referential integrity).
#
# Usage:  bash scripts/reset-cohort.sh [BASE_URL]
#
set -euo pipefail
BASE="${1:-https://hapi.fhir.org/baseR4}"
TAG="urn:prohori|demo-cohort"

for TYPE in Condition Observation Encounter Patient; do
  count=0
  while :; do
    ids=$(curl -sS -G -H 'Accept: application/fhir+json' "$BASE/$TYPE" \
      --data-urlencode "_tag=$TAG" --data-urlencode "_count=50" --data-urlencode "_elements=id" \
      | jq -r '.entry[]?.resource.id // empty')
    [ -z "$ids" ] && break
    for id in $ids; do
      curl -sS -o /dev/null -X DELETE "$BASE/$TYPE/$id" && count=$((count + 1))
    done
  done
  printf '%-11s deleted %d\n' "$TYPE" "$count"
done
