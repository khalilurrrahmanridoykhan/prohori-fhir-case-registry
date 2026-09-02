#!/usr/bin/env bash
#
# Phase A — run the by-hand FHIR REST walkthrough against a sandbox with curl.
# Mirrors the Bruno collection in bruno/ (requests 01-08).
#
# Usage:  bash scripts/phase-a.sh [BASE_URL]
# Default BASE_URL: https://hapi.fhir.org/baseR4
#
# Each run uses a unique National ID + name suffix so it works against servers
# that reject duplicate resources (HAPI-2840 on hapi.fhir.org).
#
set -euo pipefail

BASE="${1:-https://hapi.fhir.org/baseR4}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN="$(date +%s)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

hr() { printf '\n\033[1m===== %s =====\033[0m\n' "$1"; }
loc_id() { grep -i '^location:' | tr -d '\r' | sed -E 's#.*/Patient/([^/]+)/_history.*#\1#'; }

# unique patient body for this run
jq --arg sid "1994269$RUN" --arg given "Rahman-$RUN" \
  '.identifier[0].value=$sid | .name[0].given=[$given]' \
  "$DIR/fixtures/patient.json" > "$TMP/patient.json"

hr "01  GET $BASE/metadata"
curl -sS -H 'Accept: application/fhir+json' "$BASE/metadata" \
  | jq '{fhirVersion, software, patientInteractions: [.rest[0].resource[] | select(.type=="Patient") | .interaction[].code]}'

hr "02  POST $BASE/Patient  (create, id assigned by server)"
curl -sS -D "$TMP/h02" -X POST "$BASE/Patient" \
  -H 'Content-Type: application/fhir+json' -H 'Accept: application/fhir+json' \
  --data-binary @"$TMP/patient.json" -o "$TMP/create.json"
grep -iE '^HTTP|^location:|^etag:|^last-modified:' "$TMP/h02" | tr -d '\r'
PID="$(loc_id < "$TMP/h02")"
echo "patientId = $PID"
jq '{id, versionId: .meta.versionId, lastUpdated: .meta.lastUpdated}' "$TMP/create.json"

hr "03  GET $BASE/Patient/$PID  (read current version)"
curl -sS -D "$TMP/h03" -H 'Accept: application/fhir+json' "$BASE/Patient/$PID" -o "$TMP/read.json"
grep -iE '^HTTP|^etag:' "$TMP/h03" | tr -d '\r'
jq '{versionId: .meta.versionId, city: .address[0].city}' "$TMP/read.json"

hr "04  PUT $BASE/Patient/$PID  (update: city -> Narayanganj, whole resource)"
jq --arg pid "$PID" '.id=$pid | .address[0].city="Narayanganj" | .address[0].district="Narayanganj"' \
  "$TMP/create.json" > "$TMP/update.json"
curl -sS -D "$TMP/h04" -X PUT "$BASE/Patient/$PID" \
  -H 'Content-Type: application/fhir+json' -H 'Accept: application/fhir+json' \
  --data-binary @"$TMP/update.json" -o "$TMP/updated.json"
grep -iE '^HTTP|^etag:' "$TMP/h04" | tr -d '\r'
jq '{versionId: .meta.versionId, city: .address[0].city}' "$TMP/updated.json"

hr "05  GET $BASE/Patient/$PID/_history  (audit trail, newest first)"
curl -sS -H 'Accept: application/fhir+json' "$BASE/Patient/$PID/_history" \
  | jq '{type, total, entries: [.entry[] | {method: .request.method, status: .response.status, versionId: .resource.meta.versionId, city: .resource.address[0].city}]}'

hr "06  DELETE $BASE/Patient/$PID"
curl -sS -D "$TMP/h06" -X DELETE -H 'Accept: application/fhir+json' "$BASE/Patient/$PID" -o "$TMP/del.json"
grep -iE '^HTTP' "$TMP/h06" | tr -d '\r'
jq -c '[.issue[] | {severity, diagnostics}]' "$TMP/del.json" 2>/dev/null || echo "(no body)"

hr "07  GET $BASE/Patient/$PID  (expect 410 Gone)"
curl -sS -D "$TMP/h07" -H 'Accept: application/fhir+json' "$BASE/Patient/$PID" -o "$TMP/gone.json"
grep -iE '^HTTP' "$TMP/h07" | tr -d '\r'
jq -c '[.issue[] | {severity, code, diagnostics}]' "$TMP/gone.json"

hr "08  POST $BASE/Patient  (invalid gender -> OperationOutcome)"
curl -sS -D "$TMP/h08" -X POST "$BASE/Patient" \
  -H 'Content-Type: application/fhir+json' -H 'Accept: application/fhir+json' \
  --data-binary @"$DIR/fixtures/patient-invalid.json" -o "$TMP/invalid.json"
grep -iE '^HTTP' "$TMP/h08" | tr -d '\r'
jq '{resourceType, issue: [.issue[] | {severity, code, diagnostics}]}' "$TMP/invalid.json"

hr "done — patientId $PID (now deleted)"
