# Phase H — SMART launch and protected writes

Prohori has two launch paths: a standard SMART sandbox launch and a local
Keycloak OAuth/OIDC demo. The unauthenticated homepage remains a synthetic,
read-only public cohort demo. All API case writes require a bearer token.

## SMART sandbox: standalone and EHR launch

Run `cd web && npm ci && npm run dev`. The launch URL is
`http://localhost:5173/launch.html`; the registered redirect URI is
`http://localhost:5173/`. The default public client ID is `prohori-dashboard`.
Set `VITE_SMART_CLIENT_ID` for a server that assigns a different registration.

Open https://launch.smarthealthit.org, select FHIR R4 and SMART 2, select a
synthetic patient, and enter the launch URL. Use its EHR launch option to send
`iss` and `launch` to Prohori. For standalone launch, visit `/launch.html?iss=`
with the URL-encoded SMART FHIR base (or set `VITE_SMART_ISS`). The default
issuer is `https://launch.smarthealthit.org/v/r4/fhir`.

1. `launch.html` calls `fhirclient.oauth2.authorize`.
2. Discovery reads `[iss]/.well-known/smart-configuration`.
3. The browser generates OAuth state and a PKCE verifier, then sends an
   authorization request with `code_challenge_method=S256`, the FHIR `aud`,
   and the EHR's opaque `launch` value when supplied.
4. The redirect handler calls `oauth2.ready` to check state and exchange the
   code using its stored verifier. A public browser client has no secret.
5. A missing patient context stops rendering. Successful launches query
   Encounters with `patient=<launch patient>` and use the SMART client's bearer
   token for reads. Detail navigation to a different patient is refused.

The requested read scope is `patient/*.rs` (read and search, SMART 2 syntax),
plus `launch/patient openid fhirUser` and `launch` for EHR launch. The
Observation-specific `patient/Observation.rs` would not cover the dashboard's
Patient, Encounter and Condition reads. `user/*.read` is the older SMART 1
user-level read syntax; it is not equivalent to a patient compartment grant.
The dashboard may show no visits for a sandbox patient with no Encounters.

## Local Keycloak demo

```sh
colima start --cpu 4 --memory 6
docker compose -f deploy/docker-compose.yml up -d
# Wait for both URLs to return 200:
curl -f http://localhost:8080/fhir/metadata
curl -f http://localhost:8081/realms/prohori/.well-known/openid-configuration
python3 scripts/seed-smart.py
Fhir__BaseUrl=http://localhost:8080/fhir Smart__Enabled=true \
  dotnet run --project src/Prohori.Api --urls http://localhost:5279
# In another terminal:
cd web && npm run dev
```

Visit
http://localhost:5173/launch.html?iss=http%3A%2F%2Flocalhost%3A5279%2Ffhir
and sign in as `demo` / `synthetic-demo-only`. These are deliberately public
local demo credentials. The realm enforces S256 for the public dashboard
client, disables password grants, and gives access tokens the `prohori-api`
audience. Keycloak is bound to loopback and uses a persistent local volume.
Changing the import JSON does not overwrite an already imported realm.

Keycloak is not itself a SMART authorization server: its token response lacks
the SMART `patient` field. For this local demo, Prohori validates the access
token and retrieves the fixed synthetic patient's context from signed claims
via `/smart/context`. The read facade exposes only Encounter search, Patient read and the
launch patient's `$everything`; it constructs upstream searches from the
verified patient claim and does not forward caller query parameters. This
adapter does not claim full SMART server conformance or arbitrary EHR launch
context support. It is disabled unless `Smart__Enabled=true`.

HAPI itself remains a development server, accessed directly for seeding and
REST exercises. The local facade is the protected browser read surface; do
not expose this Docker setup as a production protected FHIR server.

## Access tokens, scopes and OIDC

`Prohori.Api` uses `AddJwtBearer`: OIDC discovery supplies the issuer's JWKS;
the middleware validates signature, issuer, audience and lifetime. Production
requires HTTPS metadata; Development permits the loopback HTTP issuer. Set
`Auth__Authority` to the deployment's HTTPS Keycloak realm. No validation
bypass, fixed bearer token, or browser client secret exists in application code.

An access token authorizes API calls. An OIDC ID token describes the signed-in
identity and is not an API credential. Prohori doesn't use decoded ID-token
claims for authorization. `fhirUser` identifies the user as a FHIR resource;
patient context identifies the record selected for this launch. They need not
be the same person (the local synthetic fixture intentionally uses one patient).

Both `POST /cases` and `POST /bd-core/cases`, including its dry-run variant,
and `PATCH` / `PUT /cases/{id}` require the exact whitespace-delimited scope
`user/*.write`. Missing/invalid tokens return `401`; valid tokens without that
scope return `403`. The local read facade requires `patient/*.rs` and a patient
claim. SMART sandbox tokens are for that sandbox; they are not accepted by the
Keycloak-protected API. JWT validation uses JWKS rather than a network token
introspection call on every request; revoked tokens can remain valid until
expiration. Keep token lifetimes short for a real deployment.

```sh
curl -i -X POST http://localhost:5279/cases \
  -H 'Content-Type: application/json' -d '{}' # 401
```

For existing BD-Core CI, `scripts/bd-core.sh` generates the bundle offline with
`--export-bd-core input.json output.json`; no public unauthenticated HTTP
exception is needed.

## PATCH, optimistic locking and response preferences

`/cases/{id}` addresses the underlying **Patient id**, not the Encounter id.
Read `Patient/{id}` first and retain its `ETag` / `meta.versionId`.

```sh
# TOKEN is a locally obtained Keycloak access token with user/*.write.
curl -i -X PATCH http://localhost:5279/cases/prohori-smart-demo \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/fhir+json' \
  -H 'If-Match: W/"1"' -H 'Prefer: return=representation' \
  -d '{"resourceType":"Parameters","parameter":[{"name":"operation","part":[
    {"name":"type","valueCode":"replace"},
    {"name":"path","valueString":"Patient.active"},
    {"name":"value","valueBoolean":false}]}]}'
```

Repeat with the same ETag after a successful change: expect `412 Precondition
Failed`. Re-read and reconcile before retrying; do not silently overwrite.
Missing If-Match returns `428`; malformed tags return `400`. The upstream HAPI
server applies the patch and checks the version atomically. The API preserves
its FHIR error body. HAPI 8 emits `409` with `HAPI-0974` for a stale PATCH;
only that specific version conflict is translated to `412`. HAPI PATCH also
omits ETag, so the adapter derives it from the successful versioned
`Content-Location`. Other upstream status codes are preserved. A FHIRPath patch uses a `Parameters`
resource; JSON Patch uses `application/json-patch+json`, for example
`[{"op":"replace","path":"/active","value":true}]`.

Whole-resource `PUT /cases/{id}` requires matching URL/body ids and the same
If-Match header. The original Bruno read/update sequence and `phase-a.sh` also
capture and send the version header now (Phase C originally had creation, not
an update endpoint).

`Prefer: return=representation`, `return=minimal`, and `return=OperationOutcome`
are forwarded on Patient writes. `Prefer: respond-async` is an optional
preference: these writes execute synchronously and do not advertise async
support. No fake `202` or polling URL is returned. Phase I introduces the real
`202` + `Content-Location` + polling workflow for Bulk Data export.

## Verification and conformance

Run `dotnet test --filter 'Category!=Integration'` and, under `web/`,
`npx playwright install chromium && npm test`. Unit tests use real signed RSA
JWTs with static discovery keys; they test authentication without depending on
Keycloak uptime. Browser regression tests simulate an EHR and exercise real
fhirclient redirects, state, PKCE exchange, patient filtering and denied
cross-patient navigation.

The Inferno **SMART App Launch Client** suite tests the application. The
similarly named **server** suite tests an authorization/FHIR server and cannot
certify this dashboard. Select the standalone public-client scenario, register
Prohori's redirect URI and client ID, and use Inferno's supplied FHIR issuer
as `iss`. Save the actual session link and results before claiming a pass.
Live execution evidence, including the hosted Inferno CORS limitation and diagnostic protocol pass, is recorded in `docs/phase-h-verification.md`.

## References

- [SMART client API](https://docs.smarthealthit.org/client-js/api.html)
- [SMART App Launch 2.0](https://hl7.org/fhir/smart-app-launch/2.0.0/)
- [FHIR R4 FHIRPath Patch](https://hl7.org/fhir/R4/fhirpatch.html)
- [FHIR R4 HTTP interactions](https://hl7.org/fhir/R4/http.html)
- [Keycloak containers](https://www.keycloak.org/server/containers)
- [ASP.NET Core JWT bearer authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [Inferno SMART App Launch test kit](https://github.com/inferno-framework/smart-app-launch-test-kit)
