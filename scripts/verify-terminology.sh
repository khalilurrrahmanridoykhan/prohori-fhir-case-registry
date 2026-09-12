#!/usr/bin/env bash
#
# Phase K — exercise the terminology operations a real FHIR server offers that
# the static offline validator can't: $expand a Prohori ValueSet, $validate-code
# an in-set and an out-of-set code, and $translate a legacy code to SNOMED CT.
# See docs/terminology.md for why this has to be a live server, not validator_cli.
#
# Needs: local HAPI up (docker compose -f deploy/docker-compose.yml up -d hapi)
# and terminology loaded (scripts/load-terminology.sh).
#
# Usage:  bash scripts/verify-terminology.sh [BASE_URL]
# Default BASE_URL: http://localhost:8080/fhir
#
set -euo pipefail
BASE="${1:-http://localhost:8080/fhir}"
CANON="https://prohori.health/fhir"

fail=0

# param <json> <name>  ->  the string form of Parameters.parameter[name=<name>].value[x]
param() {
  python3 -c "
import json, sys
p = json.load(sys.stdin)
for entry in p.get('parameter', []):
    if entry.get('name') == sys.argv[1]:
        for k, v in entry.items():
            if k.startswith('value'):
                print(v)
                sys.exit(0)
print('MISSING')
" "$2" <<<"$1"
}

echo "▸ \$expand bd-condition-icd11-diagnosis-valueset-fixed — expect 1D40, 1F4Z"
expansion=$(curl -sS "$BASE/ValueSet/\$expand?url=$CANON/ValueSet/bd-condition-icd11-diagnosis-valueset-fixed")
codes=$(python3 -c "import json,sys; print(sorted(c['code'] for c in json.loads(sys.argv[1])['expansion']['contains']))" "$expansion")
if [ "$codes" = "['1D40', '1F4Z']" ]; then
  echo "  ✓ expanded to $codes"
else
  echo "  ✗ unexpected expansion: $codes"; fail=1
fi

echo "▸ \$validate-code — 10828004 (Positive, in-set) — expect true"
result=$(curl -sS "$BASE/ValueSet/\$validate-code?url=$CANON/ValueSet/prohori-rdt-result-valueset&system=http://snomed.info/sct&code=10828004")
if [ "$(param "$result" result)" = "True" ]; then
  echo "  ✓ accepted"
else
  echo "  ✗ expected the in-set code to validate"; fail=1
fi

echo "▸ \$validate-code — 260415000 (Not detected, out-of-set) — expect false"
result=$(curl -sS "$BASE/ValueSet/\$validate-code?url=$CANON/ValueSet/prohori-rdt-result-valueset&system=http://snomed.info/sct&code=260415000")
if [ "$(param "$result" result)" = "False" ]; then
  echo "  ✓ rejected"
else
  echo "  ✗ expected the out-of-set code to be rejected"; fail=1
fi

echo "▸ \$translate — legacy 'pos' -> SNOMED"
result=$(curl -sS "$BASE/ConceptMap/\$translate?url=$CANON/ConceptMap/prohori-rdt-result-legacy-to-snomed&system=$CANON/CodeSystem/prohori-rdt-result-legacy&code=pos")
target=$(python3 -c "
import json, sys
p = json.loads(sys.argv[1])
for entry in p['parameter']:
    if entry['name'] == 'match':
        for part in entry['part']:
            if part['name'] == 'concept':
                print(part['valueCoding']['code'])
" "$result")
if [ "$target" = "10828004" ]; then
  echo "  ✓ translated to SNOMED $target (Positive)"
else
  echo "  ✗ expected SNOMED 10828004, got: $target"; fail=1
fi

echo "▸ \$translate — legacy 'neg' -> SNOMED"
result=$(curl -sS "$BASE/ConceptMap/\$translate?url=$CANON/ConceptMap/prohori-rdt-result-legacy-to-snomed&system=$CANON/CodeSystem/prohori-rdt-result-legacy&code=neg")
target=$(python3 -c "
import json, sys
p = json.loads(sys.argv[1])
for entry in p['parameter']:
    if entry['name'] == 'match':
        for part in entry['part']:
            if part['name'] == 'concept':
                print(part['valueCoding']['code'])
" "$result")
if [ "$target" = "260385009" ]; then
  echo "  ✓ translated to SNOMED $target (Negative)"
else
  echo "  ✗ expected SNOMED 260385009, got: $target"; fail=1
fi

echo
[ "$fail" = "0" ] && echo "Terminology verification: all expectations met" || { echo "Terminology verification: FAILED"; exit 1; }
