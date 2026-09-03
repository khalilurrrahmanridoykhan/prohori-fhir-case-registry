#!/usr/bin/env bash
#
# Validate the Prohori profile against a set of expectation fixtures using the
# official HL7 FHIR validator. Needs Java 11+.
#
#   1 conformant patient  -> expected to PASS
#   4 broken patients      -> each expected to FAIL
#
# Usage:  bash scripts/validate-ig.sh
#
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GEN="$DIR/ig/fsh-generated/resources"
TESTS="$DIR/ig/input/tests"
CACHE="${PROHORI_TOOLS:-$HOME/.fhir/validator}"
JAR="$CACHE/validator_cli.jar"
PROFILE="https://prohori.health/fhir/StructureDefinition/prohori-patient"
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

# run <file>  ->  exit 0 if the validator reports no errors, 1 otherwise
run() {
  set +e
  java -jar "$JAR" "$1" -version 4.0.1 -ig "$GEN" -profile "$PROFILE" -tx n/a >"$LOG" 2>&1
  local rc=$?
  set -e
  return $rc
}

fail=0

echo "▸ conformant patient — expect PASS"
if run "$TESTS/patient-conformant.json"; then
  echo "  ✓ passed"
else
  echo "  ✗ unexpectedly failed:"; grep -E "error|Fail" "$LOG" | head; fail=1
fi

for f in patient-no-nid patient-wrong-system patient-bad-nid patient-no-birthdate; do
  echo "▸ $f — expect FAIL"
  if run "$TESTS/$f.json"; then
    echo "  ✗ unexpectedly passed"; fail=1
  else
    echo "  ✓ rejected"
  fi
done

echo
[ "$fail" = "0" ] && echo "IG validation: all expectations met" || { echo "IG validation: FAILED"; exit 1; }
