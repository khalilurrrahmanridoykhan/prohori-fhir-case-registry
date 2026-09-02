#!/usr/bin/env bash
#
# Phase B — run the FHIR search-query catalogue against a sandbox.
# Seed first:  python3 scripts/seed-cohort.py [BASE_URL]
#
# Usage:  bash scripts/phase-b.sh [BASE_URL]
#
set -euo pipefail
BASE="${1:-https://hapi.fhir.org/baseR4}"
TAG="urn:prohori|demo-cohort"

# q <label> <resourceType> <param>...
q() {
  local label="$1" rtype="$2"; shift 2
  local args=(-sS -G -H 'Accept: application/fhir+json' "$BASE/$rtype")
  local shown="$BASE/$rtype?"
  for p in "$@"; do
    args+=(--data-urlencode "$p")
    shown+="${p}&"
  done
  printf '\n\033[1m▸ %s\033[0m\n  %s\n' "$label" "${shown%&}"
  curl "${args[@]}" | jq -r '
    "  total=\(.total // "n/a")   entries=\((.entry|length)//0)   kinds=\([.entry[]?.resource.resourceType]|unique|join(","))"
    + (if (.link // []) | any(.relation=="next") then "   [has next page]" else "" end)'
}

echo "BASE=$BASE"

# grab one real identifier from the seeded cohort for the token-exact example
SAMPLE_ID=$(curl -sS -G -H 'Accept: application/fhir+json' "$BASE/Patient" \
  --data-urlencode "_tag=$TAG" --data-urlencode "name=Khan" --data-urlencode "_count=1" \
  | jq -r '.entry[0].resource.identifier[0] | "\(.system)|\(.value)"')

# ---- string params ----
q "string: name starts-with 'na'"      Patient "name=na"            "_tag=$TAG"
q "string :exact (single name part)"   Patient "name:exact=Khan"    "_tag=$TAG"
q "string :exact on family"            Patient "family:exact=Khan"  "_tag=$TAG"
q "string :contains"                   Patient "name:contains=hos"  "_tag=$TAG"
q "string: address-city = Dhaka"       Patient "address-city=Dhaka" "_tag=$TAG"

# ---- token params ----
q "token exact: identifier system|value" Patient "identifier=$SAMPLE_ID"
q "token: gender = female"             Patient "gender=female"       "_tag=$TAG"
q "token :not (gender != male)"        Patient "gender:not=male"     "_tag=$TAG"
q "token: Observation.code = LOINC dengue NS1" Observation "code=http://loinc.org|42239-4" "_tag=$TAG"
q "token: Observation value = SNOMED Positive" Observation "value-concept=http://snomed.info/sct|10828004" "_tag=$TAG"
q "composite: positive dengue NS1"     Observation "code-value-concept=http://loinc.org|42239-4\$http://snomed.info/sct|10828004" "_tag=$TAG"
q "token: Condition.code = SNOMED dengue" Condition "code=http://snomed.info/sct|38362002" "_tag=$TAG"

# ---- date params + prefixes ----
q "date: birthdate exact"              Patient "birthdate=1995-06-15" "_tag=$TAG"
q "date range: born in the 1990s"      Patient "birthdate=ge1990-01-01" "birthdate=le1999-12-31" "_tag=$TAG"
q "date gt: born after 2010 (children)" Patient "birthdate=gt2010-01-01" "_tag=$TAG"
q "date ge: visits from 15 Aug 2026"   Observation "date=ge2026-08-15" "_tag=$TAG"

# ---- modifiers ----
q "modifier :missing=true (no deceased date)"  Patient "death-date:missing=true" "_tag=$TAG"
q "modifier :missing=true (no GP recorded)"    Patient "general-practitioner:missing=true" "_tag=$TAG"

# ---- includes ----
q "_include: observations + their patients" Observation "code=http://loinc.org|42239-4" "_include=Observation:subject" "_tag=$TAG"
q "_revinclude: patients + their observations" Patient "_tag=$TAG" "_revinclude=Observation:subject"

# ---- chaining ----
q "chained: observations where subject.name = Khan" Observation "subject.name=Khan" "_tag=$TAG"
q "_has: patients WITH a positive result" Patient "_has:Observation:subject:value-concept=http://snomed.info/sct|10828004" "_tag=$TAG"
q "_has: patients WITH a dengue Condition" Patient "_has:Condition:subject:code=http://snomed.info/sct|38362002" "_tag=$TAG"

# ---- result control ----
q "_summary=count (no entries, just total)" Patient "_tag=$TAG" "_summary=count"
q "_elements (thin projection)"        Patient "_tag=$TAG" "_elements=name,birthDate"
q "_sort + _count (2 newest conditions)" Condition "_tag=$TAG" "_sort=-recorded-date" "_count=2"
q "pagination: _count=3 (follow [has next page])" Patient "_tag=$TAG" "_count=3"

printf '\n\033[1m▸ operation: full case in one call — Patient/{id}/$everything\033[0m\n'
PID=$(curl -sS -G -H 'Accept: application/fhir+json' "$BASE/Patient" \
  --data-urlencode "_tag=$TAG" --data-urlencode "name=Khan" --data-urlencode "_count=1" \
  | jq -r '.entry[0].resource.id')
echo "  $BASE/Patient/$PID/\$everything"
curl -sS -H 'Accept: application/fhir+json' "$BASE/Patient/$PID/\$everything" \
  | jq -r '"  total=\(.total)   kinds=\([.entry[].resource.resourceType]|unique|join(","))"'

printf '\n\033[1mdone\033[0m\n'
