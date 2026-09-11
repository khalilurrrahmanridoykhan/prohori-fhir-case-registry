#!/usr/bin/env bash
# Real, isolated Keycloak + HAPI integration; requires Docker, .NET 8, Python 3, OpenSSL.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build -c Release
if [[ ! -f .bulk/client-key.pem ]]; then
  dotnet src/Prohori.BulkClient/bin/Release/net8.0/Prohori.BulkClient.dll --keygen .bulk
fi
python3 scripts/configure-bulk.py
docker compose -f deploy/docker-compose.bulk.yml up -d hapi
# Keycloak imports the public key into a disposable realm on first startup.
docker compose -f deploy/docker-compose.bulk.yml up -d --force-recreate keycloak
mkdir -p .bulk
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5280 \
  Fhir__BaseUrl=http://localhost:8090/fhir \
  Auth__Authority=http://localhost:8091/realms/prohori-bulk Bulk__Enabled=true \
  dotnet src/Prohori.Api/bin/Release/net8.0/Prohori.Api.dll > .bulk/api.log 2>&1 &
bulk_api_pid=$!
trap 'kill "$bulk_api_pid" 2>/dev/null || true' EXIT
python3 scripts/verify-bulk.py "$bulk_api_pid"
