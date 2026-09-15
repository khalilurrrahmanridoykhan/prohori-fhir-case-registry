#!/usr/bin/env bash
#
# Phase O — prove the Subscription pipeline is real: register a rest-hook Subscription
# on local HAPI, create a matching Observation, and confirm HAPI actually calls
# Prohori.Subscriber's /notify (not just that the code compiles). Consent and AuditEvent
# — the other two Phase O pieces — are proven live in CaseSubmissionIntegrationTests
# (Category=Integration): they're created inside the same authenticated transaction every
# case write already goes through, so the honest place to verify them for real is that
# same path, not a second, parallel one here.
#
# Needs: .NET 8 SDK, Docker/Colima (local HAPI already up — see deploy/docker-compose.yml).
#
#   docker compose -f deploy/docker-compose.yml up -d hapi
#   bash scripts/verify-realtime.sh
#
set -euo pipefail
cd "$(dirname "$0")/.."

FHIR_BASE="${1:-http://localhost:8080/fhir}"
SUB_HOST="http://localhost:5300"
# HAPI (in Docker) must reach this service running on the host to deliver the
# rest-hook callback — host.docker.internal, not localhost. See docker-compose.yml's
# extra_hosts (needed on Linux/CI; Docker Desktop maps this automatically).
SUB_PUBLIC="http://host.docker.internal:5300"
# Fixed, not randomized per run: the Subscription's conditional create (If-None-Exist on
# criteria) only creates when NONE already exists — it never updates an existing match's
# channel.header. A fresh random key on every run would only take effect on the very first
# run against a given server; every later run would still be rejected by an already-active
# Subscription still carrying the old key (found by hitting exactly that on a second run
# against a HAPI instance kept up between them — see docs/realtime-provenance-consent.md).
NOTIFY_KEY="verify-realtime-key"

dotnet build -c Release src/Prohori.Subscriber
Fhir__BaseUrl="$FHIR_BASE" Subscriber__PublicUrl="$SUB_PUBLIC" Subscriber__NotifyKey="$NOTIFY_KEY" ASPNETCORE_URLS="$SUB_HOST" \
  dotnet src/Prohori.Subscriber/bin/Release/net8.0/Prohori.Subscriber.dll > /tmp/prohori-subscriber.log 2>&1 &
sub_pid=$!
trap 'kill "$sub_pid" 2>/dev/null || true' EXIT

echo "▸ waiting for Prohori.Subscriber"
for i in $(seq 1 20); do
  code=$(curl -s -o /dev/null -w '%{http_code}' "$SUB_HOST/health" || echo 000)
  [ "$code" = "200" ] && break
  sleep 1
done

fail=0

echo "▸ Subscription registered on HAPI, criteria matches"
sub_json=$(curl -sS "$FHIR_BASE/Subscription?criteria=$(python3 -c "import urllib.parse,sys; print(urllib.parse.quote(sys.argv[1], safe=''))" "Observation?_tag=urn:prohori|demo-cohort")")
sub_count=$(python3 -c "import json,sys; print(json.loads(sys.argv[1]).get('total', 0))" "$sub_json")
if [ "$sub_count" -ge 1 ]; then
  echo "  ✓ found $sub_count matching Subscription(s)"
else
  echo "  ✗ no Subscription found with the expected criteria"; fail=1
fi

echo "▸ waiting for HAPI to activate it (requested -> active)"
status=""
for i in $(seq 1 20); do
  status=$(curl -sS "$FHIR_BASE/Subscription?criteria=$(python3 -c "import urllib.parse,sys; print(urllib.parse.quote(sys.argv[1], safe=''))" "Observation?_tag=urn:prohori|demo-cohort")" \
    | python3 -c "import json,sys; b=json.loads(sys.stdin.read()); print(b['entry'][0]['resource']['status'] if b.get('entry') else 'none')")
  [ "$status" = "active" ] && break
  sleep 1
done
if [ "$status" = "active" ]; then
  echo "  ✓ Subscription is active"
else
  echo "  ✗ Subscription never reached 'active' (last seen: $status)"; fail=1
fi

echo "▸ posting a tagged Observation directly to HAPI — the one thing the Subscription matches on"
obs_id=$(curl -sS -X POST "$FHIR_BASE/Observation" -H 'Content-Type: application/fhir+json' -d '{
  "resourceType": "Observation",
  "meta": {"tag": [{"system": "urn:prohori", "code": "demo-cohort"}]},
  "status": "final",
  "code": {"coding": [{"system": "http://loinc.org", "code": "42239-4"}]}
}' | python3 -c "import json,sys; print(json.load(sys.stdin)['id'])")
echo "  -> Observation/$obs_id"

echo "▸ waiting for the rest-hook notification to reach /notify and broadcast"
seen=""
for i in $(seq 1 20); do
  seen=$(curl -s "$SUB_HOST/debug/last-notification" || true)
  echo "$seen" | grep -q "\"$obs_id\"" && break
  sleep 1
done
if echo "$seen" | grep -q "\"$obs_id\""; then
  echo "  ✓ notification received: $seen"
else
  echo "  ✗ Observation/$obs_id never showed up in Prohori.Subscriber's last notification (got: $seen)"; fail=1
fi

echo
if [ "$fail" = "0" ]; then
  echo "Realtime verification: all expectations met"
else
  echo "Realtime verification: FAILED"; cat /tmp/prohori-subscriber.log; exit 1
fi
