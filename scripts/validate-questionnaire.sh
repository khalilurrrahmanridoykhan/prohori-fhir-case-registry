#!/usr/bin/env bash
#
# Phase J — build the Prohori field-intake Questionnaire (C#, not FSH — see
# DECISIONS.md) and validate it against the base FHIR R4 spec. No custom IG:
# a Questionnaire has no separate "real-world instance" to constrain, this
# resource IS the artifact.
#
# Needs: .NET 8 SDK, Java 11+ (for the validator).
#
#   bash scripts/validate-questionnaire.sh
#
set -euo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$DIR/.sdc"; mkdir -p "$OUT"
JAR="${PROHORI_TOOLS:-$HOME/.fhir/validator}/validator_cli.jar"

[ -f "$JAR" ] || { echo "Downloading validator_cli.jar…"; mkdir -p "$(dirname "$JAR")"; \
  curl -fsSL -o "$JAR" "https://github.com/hapifhir/org.hl7.fhir.core/releases/download/6.5.19/validator_cli.jar"; }

echo "▸ building the Questionnaire (offline)…"
dotnet run --project "$DIR/src/Prohori.Api" --no-launch-profile -- \
  --export-questionnaire "$OUT/questionnaire.json"
echo "  -> $OUT/questionnaire.json"

echo "▸ validating against base FHIR R4…"
java -jar "$JAR" "$OUT/questionnaire.json" -version 4.0.1 -tx n/a 2>&1 \
  | tee "$OUT/validation.log" | grep -E 'Success|FAILURE|Error @'
grep -qE '^Success: 0 errors' "$OUT/validation.log" || { echo "  validation FAILED"; exit 1; }
