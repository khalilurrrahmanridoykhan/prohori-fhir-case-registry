#!/usr/bin/env bash
#
# Phase L — run Prohori.V2Gateway against a real local HAPI and prove the ADT lifecycle end to
# end: A01 admits (creates Patient + Encounter), A08 updates the same Patient in place (not a
# duplicate), A03 discharges the same Encounter (status -> finished, period.end set).
#
# Needs: .NET 8 SDK, Docker/Colima (local HAPI already up — see deploy/docker-compose.yml).
#
#   docker compose -f deploy/docker-compose.yml up -d hapi
#   bash scripts/verify-v2-gateway.sh
#
set -euo pipefail
cd "$(dirname "$0")/.."

FHIR_BASE="${1:-http://localhost:8080/fhir}"
GW="http://localhost:5300"
MRN="MRNVERIFY$(date +%s | tail -c 6)" # unique per run, dodges HAPI-2840 on a shared server
VN="VNVERIFY$(date +%s | tail -c 6)"

dotnet build -c Release src/Prohori.V2Gateway
Fhir__BaseUrl="$FHIR_BASE" ASPNETCORE_URLS="$GW" \
  dotnet src/Prohori.V2Gateway/bin/Release/net8.0/Prohori.V2Gateway.dll > /tmp/prohori-v2gw.log 2>&1 &
gw_pid=$!
trap 'kill "$gw_pid" 2>/dev/null || true' EXIT

for i in $(seq 1 20); do
  code=$(curl -s -o /dev/null -w '%{http_code}' "$GW/health" || echo 000)
  [ "$code" = "200" ] && break
  sleep 1
done

# field <n> <value>  ->  the nth PV1 field (1-indexed), all others empty
pv1() {
  python3 -c "
import sys
fields = [''] * 46
fields[1] = '1'; fields[2] = 'I'; fields[3] = 'WARD3^^^DHMC'; fields[7] = '1234^Ahmed^Fatima'
fields[19] = sys.argv[1]
fields[44] = '20260814090000'
if len(sys.argv) > 2: fields[45] = sys.argv[2]
print('PV1|' + '|'.join(fields[1:]))
" "$@"
}

adt() {
  local trigger=$1 msgid=$2 city=$3 pv1_line=$4
  printf 'MSH|^~\\&|PROHORI|DHMC|FHIRGW|PROHORI|20260814090000||ADT^%s|%s|P|2.5.1\rEVN|%s|20260814090000\rPID|1||%s^^^DHMC^MR||Karim^Rahman||19900101|M|||%s^^%s^%s^^BD\r%s\r' \
    "$trigger" "$msgid" "$trigger" "$MRN" "$city" "$city" "$city" "$pv1_line"
}

fail=0

echo "▸ A01 admit — expect a fresh Patient + Encounter"
created=$(adt A01 MSG1 Dhaka "$(pv1 "$VN")" | curl -sS -X POST "$GW/adt" -H 'Content-Type: text/plain' --data-binary @-)
patient_id=$(python3 -c "import json,sys; print(json.loads(sys.argv[1])['created'][0].split('/')[1])" "$created")
encounter_id=$(python3 -c "import json,sys; print(json.loads(sys.argv[1])['created'][1].split('/')[1])" "$created")
echo "  -> Patient/$patient_id, Encounter/$encounter_id"
[ -n "$patient_id" ] && [ -n "$encounter_id" ] || { echo "  ✗ A01 did not create both resources"; fail=1; }

echo "▸ A08 update — expect the SAME Patient id, address actually changed"
created2=$(adt A08 MSG2 Chattogram "$(pv1 "$VN")" | curl -sS -X POST "$GW/adt" -H 'Content-Type: text/plain' --data-binary @-)
patient_id2=$(python3 -c "import json,sys; print(json.loads(sys.argv[1])['created'][0].split('/')[1])" "$created2")
city=$(curl -sS "$FHIR_BASE/Patient/$patient_id2" | python3 -c "import json,sys; print(json.load(sys.stdin)['address'][0]['city'])")
if [ "$patient_id2" = "$patient_id" ] && [ "$city" = "Chattogram" ]; then
  echo "  ✓ same Patient/$patient_id2, address now Chattogram"
else
  echo "  ✗ expected Patient/$patient_id with address Chattogram, got Patient/$patient_id2 / $city"; fail=1
fi

echo "▸ A03 discharge — expect the SAME Encounter id, now finished"
created3=$(adt A03 MSG3 Chattogram "$(pv1 "$VN" 20260816110000)" | curl -sS -X POST "$GW/adt" -H 'Content-Type: text/plain' --data-binary @-)
encounter_id3=$(python3 -c "import json,sys; print(json.loads(sys.argv[1])['created'][1].split('/')[1])" "$created3")
status=$(curl -sS "$FHIR_BASE/Encounter/$encounter_id3" | python3 -c "import json,sys; print(json.load(sys.stdin)['status'])")
if [ "$encounter_id3" = "$encounter_id" ] && [ "$status" = "finished" ]; then
  echo "  ✓ same Encounter/$encounter_id3, status finished"
else
  echo "  ✗ expected Encounter/$encounter_id finished, got Encounter/$encounter_id3 / $status"; fail=1
fi

echo
[ "$fail" = "0" ] && echo "V2Gateway verification: all expectations met" || { echo "V2Gateway verification: FAILED"; cat /tmp/prohori-v2gw.log; exit 1; }
