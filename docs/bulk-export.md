# Backend Services and Bulk Data

Prohori's .NET backend client authenticates without a user, runs a real HAPI R4
Bulk Data job, downloads NDJSON, and maintains a local snapshot with cases and
RDT positivity per division. All example records are synthetic.

## Run the complete verification

Prerequisites: Docker Compose, .NET 8, Python 3 and OpenSSL. On macOS, start
Docker Desktop or Colima first. Reserve loopback ports 8090, 8091 and 5280.

```bash
bash scripts/verify-bulk.sh
```

The script builds the solution, creates an RSA key if absent, imports its public
JWKS into a disposable Keycloak realm, starts HAPI and the API, seeds two visits,
and exercises full and incremental exports. It checks signature, expiry,
audience, assertion replay, scope enforcement, protected downloads, manifest
shape and aggregate results. The API child stops on exit; containers remain for
inspection. CI runs the same script as a required job and removes its containers.

Outputs live under ignored `.bulk/verify-*/`; `.bulk/evidence/` contains only
synthetic manifests and aggregate JSON for CI artifacts. Private keys, realm
configuration and tokens are never uploaded. The script resets the two synthetic
visits in the isolated lab; it never writes to the public or DGHS sandboxes.

## Run the client yourself

First-time setup, from the repository root:

```bash
dotnet run --project src/Prohori.BulkClient -- --keygen .bulk
python3 scripts/configure-bulk.py
docker compose -f deploy/docker-compose.bulk.yml up -d
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5280 \
  Fhir__BaseUrl=http://localhost:8090/fhir \
  Auth__Authority=http://localhost:8091/realms/prohori-bulk Bulk__Enabled=true \
  dotnet run --no-launch-profile --project src/Prohori.Api
```

Wait for HAPI `/fhir/metadata` and the Keycloak realm discovery endpoint to
respond, then in another terminal:

```bash
python3 scripts/seed-bulk.py
dotnet run --project src/Prohori.BulkClient
cat .bulk/export/aggregate.json
```

Key generation refuses to overwrite an existing private key. On later runs,
reuse it and skip `--keygen`. After intentionally rotating the key and regenerating
the realm, recreate the disposable Keycloak container with `up -d --force-recreate
keycloak`; restart the API to refresh issuer metadata. Never commit the private
key or share it with the server. `.bulk/jwks.json` is the public registration
artifact; Keycloak stores that JWKS by value (`use.jwks.string` / `jwks.string`).

## Authentication and the async sequence

1. The client signs a short-lived JWT using RS384 (RSA PKCS#1 v1.5 + SHA-384).
   Its `iss` and `sub` are the registered client ID, `aud` is the exact token
   endpoint, `exp` is five minutes after `iat`, and `jti` is unique per assertion.
2. It posts `grant_type=client_credentials`, `scope=system/*.read`,
   `client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer`
   and the assertion to Keycloak. There is no user, browser or refresh token.
3. The gateway validates the access token's signature, issuer, audience
   (`prohori-api`), lifetime and exact `system/*.read` scope.
4. `GET /bulk/fhir/Group/prohori-cohort/$export` with `Prefer: respond-async`
   returns **202** and an absolute `Content-Location`. HAPI executes the export.
5. Authenticated polling receives **202** while running and **200** with a
   manifest when complete. The client honors `Retry-After`, including on 429,
   refreshes expiring access tokens and stops at its overall timeout.
6. The client downloads each protected file and parses one FHIR resource per
   line using Firely. Only after all files parse does it replace local state
   and advance the checkpoint.

This lab uses the plan's legacy SMART `system/*.read` permission syntax and
advertises `permission-v1`; it does not claim SMART v2 granular scope support.
The API's `/bulk/fhir/.well-known/smart-configuration` describes this backend
flow. RS384 is the **client assertion** algorithm, independent of the algorithm
Keycloak uses to sign its access tokens.

Example manifest (URLs and timestamp vary):

```json
{
  "transactionTime": "2026-09-10T03:00:00Z",
  "request": "http://localhost:5280/bulk/fhir/Group/prohori-cohort/$export?_type=Patient,Encounter,Observation,Condition",
  "requiresAccessToken": true,
  "output": [
    { "type": "Patient", "url": "http://localhost:5280/bulk/jobs/JOB/files/0" },
    { "type": "Observation", "url": "http://localhost:5280/bulk/jobs/JOB/files/1" }
  ],
  "error": []
}
```

File count is server-selected: more than one file may contain the same resource
type. Empty output is valid. A nonempty `error` or `deleted` array makes the
client fail without advancing its snapshot; partial success is not success.

## Export scope and incremental pulls

| Client option | FHIR request / meaning |
| --- | --- |
| `--mode group --group prohori-cohort` (default) | `Group/{id}/$export`, patient cohort membership |
| `--mode patient` | `Patient/$export`, all patient compartments |
| `--mode system` | `$export`, system scope for the requested resource types |
| `--type Patient,Encounter,Observation,Condition` | `_type`, the supported four clinical resource types |
| `--type-filter 'Observation?status=final'` | `_typeFilter`, repeatable FHIR search filter, evaluated by HAPI |
| `--since TIMESTAMP` | `_since`, changed resources since a timezone-qualified instant |
| fixed NDJSON | `_outputFormat=application/fhir+ndjson` |

Group full/delta exports are the acceptance gate. Patient/system routes and
filter forwarding are provided, but support for particular filter expressions
depends on HAPI. Keep the same cohort, types and filters across deltas.

Use the **previous manifest's `transactionTime`**, never the client's completion
time, as the next watermark:

```bash
bulk_since=$(python3 -c 'import json; print(json.load(open(".bulk/export/checkpoint.json"))["transactionTime"])')
dotnet run --project src/Prohori.BulkClient -- --since "$bulk_since"
```

An incremental run requires an existing full snapshot in the same output
directory. Resources upsert by `resourceType/id`; older versions cannot replace
newer versions. Unchanged patients and encounters stay available for aggregation.
The client rejects a changed export scope, a gap after its stored watermark,
or a backwards server watermark. An overlapping replay is safe. Full exports
replace the snapshot. The output directory is locked against concurrent writers.

`aggregate.json` reports snapshot and download counts, patients, encounters,
RDT observations, and division rows. A **case** is one Encounter (a field visit),
not a unique person or confirmed diagnosis. Positivity is positive RDT observations
divided by positive plus negative observations, times 100. Unknown results are
reported separately and excluded from that denominator; an empty denominator
produces `null`. Division comes from the patient's first address `state`.

## Boundaries

- Keycloak provides OAuth authentication; the opt-in API gateway provides the
  Bulk Data authorization boundary. HAPI performs the actual export. HAPI itself
  is reachable without authentication only on loopback for lab administration.
  This is not a native HAPI SMART server or a publicly deployed bulk service.
- HAPI 8.0.0 and Keycloak 26.6.0 run in an isolated Compose project. The deployed
  application keeps bulk disabled by default. External endpoints must use HTTPS;
  HTTP is accepted only for loopback development.
- Polling jobs and file mappings belong to the authenticated subject, expire
  after 24 hours, and live in memory (maximum 100 jobs). API restarts invalidate
  them. Multiple API replicas require a shared durable registry.
- Upstream URLs are restricted to the configured HAPI status and Binary paths;
  downloads and polling never follow redirects. The console refuses URLs on a
  different origin. Gateway credentials are never forwarded to HAPI.
- Aggregation targets a small, stable cohort with relative patient references.
  It holds the snapshot in memory. Deletions and changing cohort/filter membership
  require periodic full refreshes; `_since` alone cannot infer removals. A server
  deletion manifest fails closed. This is not a general deletion-aware warehouse.
- HAPI's batch scheduler may take several minutes even for seven resources.
  Default export timeout is ten minutes. Local exports and keys stay in `.bulk/`;
  protect that directory if adapting the tool beyond synthetic data.

References: [Bulk Data Access STU2](https://hl7.org/fhir/uv/bulkdata/STU2/export.html),
[SMART Backend Services authorization](https://hl7.org/fhir/uv/bulkdata/STU1.0.1/authorization/index.html),
[HAPI starter configuration](https://github.com/hapifhir/hapi-fhir-jpaserver-starter/wiki/Configuration).
