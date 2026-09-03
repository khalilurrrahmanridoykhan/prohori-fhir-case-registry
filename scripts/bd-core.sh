#!/usr/bin/env bash
#
# Phase F — build a BD-Core-FHIR-IG conformant Bundle, validate it against the
# bd.fhir.core package, and (with --submit) POST it to the DGHS sandbox.
#
# Needs: .NET 8 SDK, Java 11+ (for the validator).
#
#   bash scripts/bd-core.sh            # build + validate
#   bash scripts/bd-core.sh --submit   # ... then submit to sandbox.fhir.dghs.gov.bd
#
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$DIR/.bd-core"; mkdir -p "$OUT"
SANDBOX="https://sandbox.fhir.dghs.gov.bd/fhir"
PKG_URL="https://fhir.dghs.gov.bd/core/package.tgz"
PKG="$OUT/bd.fhir.core.tgz"
JAR="${PROHORI_TOOLS:-$HOME/.fhir/validator}/validator_cli.jar"
PORT=5399

[ -f "$PKG" ] || { echo "Downloading bd.fhir.core package…"; curl -fsSL "$PKG_URL" -o "$PKG"; }
[ -f "$JAR" ] || { echo "Downloading validator_cli.jar…"; mkdir -p "$(dirname "$JAR")"; \
  curl -fsSL -o "$JAR" "https://github.com/hapifhir/org.hl7.fhir.core/releases/download/6.5.19/validator_cli.jar"; }

REQUEST='{
  "patient": { "nationalId": "19942691012345678", "nameEnglish": "Rahman Khan",
    "nameBangla": "রহমান খান", "gender": "male", "birthDate": "1995-06-15",
    "divisionCode": "30", "districtCode": "3026", "upazilaCode": "10040028" },
  "disease": "dengue", "rdtResult": "positive", "visitDate": "2026-08-14T09:20:00+06:00",
  "facility": { "code": "10000033", "name": "Dhaka Medical College Hospital" },
  "practitionerCode": "CHW-2201"
}'

echo "▸ building the Bundle (Prohori.Api dry-run)…"
dotnet run --project "$DIR/src/Prohori.Api" --no-launch-profile --urls "http://localhost:$PORT" \
  > "$OUT/api.log" 2>&1 &
API=$!
trap 'kill $API 2>/dev/null || true' EXIT
for _ in $(seq 1 30); do curl -sf -o /dev/null "http://localhost:$PORT/health" && break; sleep 1; done
curl -sf -X POST "http://localhost:$PORT/bd-core/cases?dryRun=true" \
  -H 'Content-Type: application/json' -d "$REQUEST" -o "$OUT/bundle.json"
kill $API 2>/dev/null || true; trap - EXIT
echo "  -> $OUT/bundle.json ($(jq -r '[.entry[].resource.resourceType] | join(", ")' "$OUT/bundle.json"))"

echo "▸ validating against bd.fhir.core…"
java -jar "$JAR" "$OUT/bundle.json" -version 4.0.1 -ig "$PKG" -tx n/a 2>&1 \
  | tee "$OUT/validation.log" | grep -E 'Success|FAILURE|Error @'
grep -qE '^Success: 0 errors' "$OUT/validation.log" || { echo "  validation FAILED"; exit 1; }

if [ "${1:-}" = "--submit" ]; then
  echo "▸ submitting to $SANDBOX …"
  code=$(curl -sS -m 90 -X POST "$SANDBOX" -H 'Content-Type: application/fhir+json' \
    --data-binary @"$OUT/bundle.json" -o "$OUT/sandbox-response.json" -w '%{http_code}')
  echo "  HTTP $code"
  jq -r '.entry[]? | "  \(.response.status)  \(.response.location)"' "$OUT/sandbox-response.json"
fi
