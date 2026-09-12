#!/usr/bin/env bash
#
# Validate the Prohori profiles against expectation fixtures using the
# official HL7 FHIR validator. Needs Java 11+.
#
#   ProhoriPatient:      1 conformant  -> PASS · 4 broken -> FAIL
#   ProhoriObservation:  1 conformant  -> PASS · 1 broken (structural) -> FAIL
#     (Phase K adds required bindings to Prohori's own SNOMED/LOINC ValueSets —
#      but -tx n/a means the *offline* validator can't actually enforce them
#      against external code systems it has no local definition for; that
#      binding enforcement is proved live against local HAPI instead, by
#      scripts/verify-terminology.sh. See docs/terminology.md.)
#
# Usage:  bash scripts/validate-ig.sh
#
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GEN="$DIR/ig/fsh-generated/resources"
TESTS="$DIR/ig/input/tests"
CACHE="${PROHORI_TOOLS:-$HOME/.fhir/validator}"
JAR="$CACHE/validator_cli.jar"
PATIENT_PROFILE="https://prohori.health/fhir/StructureDefinition/prohori-patient"
OBSERVATION_PROFILE="https://prohori.health/fhir/StructureDefinition/prohori-observation"
VER="6.5.19"

mkdir -p "$CACHE"
if [ ! -f "$JAR" ]; then
  echo "Downloading validator_cli.jar $VER ..."
  curl -fsSL -o "$JAR" \
    "https://github.com/hapifhir/org.hl7.fhir.core/releases/download/$VER/validator_cli.jar"
fi

if [ ! -d "$GEN" ]; then
  echo "No generated profile at $GEN — run:  (cd ig && sushi . --snapshot)" >&2
  exit 1
fi

LOG=/tmp/prohori-val.log

# run <file> <profile>  ->  exit 0 if the validator reports no errors, 1 otherwise
run() {
  set +e
  java -jar "$JAR" "$1" -version 4.0.1 -ig "$GEN" -profile "$2" -tx n/a >"$LOG" 2>&1
  local rc=$?
  set -e
  return $rc
}

fail=0

echo "▸ conformant patient — expect PASS"
if run "$TESTS/patient-conformant.json" "$PATIENT_PROFILE"; then
  echo "  ✓ passed"
else
  echo "  ✗ unexpectedly failed:"; grep -E "error|Fail" "$LOG" | head; fail=1
fi

for f in patient-no-nid patient-wrong-system patient-bad-nid patient-no-birthdate; do
  echo "▸ $f — expect FAIL"
  if run "$TESTS/$f.json" "$PATIENT_PROFILE"; then
    echo "  ✗ unexpectedly passed"; fail=1
  else
    echo "  ✓ rejected"
  fi
done

echo "▸ conformant observation (LOINC test code + SNOMED result) — expect PASS"
if run "$TESTS/observation-conformant.json" "$OBSERVATION_PROFILE"; then
  echo "  ✓ passed"
else
  echo "  ✗ unexpectedly failed:"; grep -E "error|Fail" "$LOG" | head; fail=1
fi

echo "▸ observation-missing-subject — expect FAIL (structural: subject is 1..1)"
if run "$TESTS/observation-missing-subject.json" "$OBSERVATION_PROFILE"; then
  echo "  ✗ unexpectedly passed"; fail=1
else
  echo "  ✓ rejected"
fi

echo
[ "$fail" = "0" ] && echo "IG validation: all expectations met" || { echo "IG validation: FAILED"; exit 1; }
