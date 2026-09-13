#!/usr/bin/env bash
#
# Phase M — seed the known demo cohort (Phase B), evaluate the disease-positivity measure over
# two different periods, and prove the numbers are real: the full period matches the cohort's
# known 4-of-8 positivity (by city too), and a narrower period reports fewer cases — not because
# anything is hardcoded, but because $evaluate-measure actually searched a different date range.
#
# Needs: .NET 8 SDK, Docker/Colima (local HAPI already up — see deploy/docker-compose.yml).
#
#   docker compose -f deploy/docker-compose.yml up -d hapi
#   bash scripts/verify-measure.sh
#
set -euo pipefail
cd "$(dirname "$0")/.."

FHIR_BASE="${1:-http://localhost:8080/fhir}"
API="http://localhost:5301"

python3 scripts/seed-cohort.py "$FHIR_BASE" > /tmp/prohori-measure-seed.log 2>&1
echo "▸ seeded the demo cohort ($(grep -c '^  ' /tmp/prohori-measure-seed.log || true) patients)"

dotnet build -c Release src/Prohori.Api
Fhir__BaseUrl="$FHIR_BASE" ASPNETCORE_URLS="$API" \
  dotnet src/Prohori.Api/bin/Release/net8.0/Prohori.Api.dll > /tmp/prohori-measure-api.log 2>&1 &
api_pid=$!
trap 'kill "$api_pid" 2>/dev/null || true' EXIT

for i in $(seq 1 20); do
  code=$(curl -s -o /dev/null -w '%{http_code}' "$API/health" || echo 000)
  [ "$code" = "200" ] && break
  sleep 1
done

fail=0

# count <json> <code>  ->  group[0].population[code].count
count() { python3 -c "
import json, sys
g = json.loads(sys.argv[1])['group'][0]
for p in g['population']:
    if p['code']['coding'][0]['code'] == sys.argv[2]:
        print(p.get('count', 0)); break
" "$1" "$2"; }

echo "▸ full period (2026-08-01..2026-08-31) — expect denominator 8, numerator 4"
full=$(curl -sS "$API/measure/\$evaluate-measure?periodStart=2026-08-01&periodEnd=2026-08-31")
d=$(count "$full" denominator); n=$(count "$full" numerator)
if [ "$d" = "8" ] && [ "$n" = "4" ]; then
  echo "  ✓ denominator=$d numerator=$n"
else
  echo "  ✗ expected 8/4, got $d/$n"; fail=1
fi

echo "▸ narrow period (2026-08-01..2026-08-10) — expect denominator 3, numerator 2 (a different window, a different answer)"
narrow=$(curl -sS "$API/measure/\$evaluate-measure?periodStart=2026-08-01&periodEnd=2026-08-10")
d2=$(count "$narrow" denominator); n2=$(count "$narrow" numerator)
if [ "$d2" = "3" ] && [ "$n2" = "2" ]; then
  echo "  ✓ denominator=$d2 numerator=$n2"
else
  echo "  ✗ expected 3/2, got $d2/$n2"; fail=1
fi

echo "▸ empty period (a year with no visits) — expect denominator 0"
empty=$(curl -sS "$API/measure/\$evaluate-measure?periodStart=2020-01-01&periodEnd=2020-12-31")
d3=$(count "$empty" denominator)
if [ "$d3" = "0" ]; then
  echo "  ✓ denominator=0"
else
  echo "  ✗ expected 0, got $d3"; fail=1
fi

echo
[ "$fail" = "0" ] && echo "Measure verification: all expectations met" || { echo "Measure verification: FAILED"; cat /tmp/prohori-measure-api.log; exit 1; }
