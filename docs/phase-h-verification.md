# Phase H verification — 2026-09-09

## Results

| Check | Result | Evidence |
| --- | --- | --- |
| .NET build and unit/API tests | 42 passed, 0 failed | `dotnet test --filter 'Category!=Integration'` |
| Browser regressions + live local flow | 4 passed | `PROHORI_LIVE_SMART=1 npm test -- --workers=1` under `web/` |
| Production web build | Passed | `npm run build` |
| BD-Core validation after offline export change | 0 errors, 9 warnings, 8 notes | `bash scripts/bd-core.sh` |
| Real API POST without token | 401 | curl against localhost:5279 |
| Wrong scope, expired token, wrong audience | 403 / 401 / 401 | Signed-RSA JWT endpoint tests |
| Real Keycloak code + PKCE login | Passed | [Screenshot](images/phase-h-keycloak.png) |
| Real HAPI FHIRPath Patch + stale replay | 200 then 412 | `web/tests/local-smart.spec.ts` |
| Real HAPI JSON Patch + respond-async preference | Synchronous 200 | Same live test; no async job advertised |
| SMART sandbox EHR launch | Passed; 11 patient-scoped encounters | [Screenshot](images/phase-h-sandbox.png), [launcher video](videos/phase-h-ehr-launch.webm), [dashboard video](videos/phase-h-ehr-dashboard.webm) |
| SMART sandbox patient standalone launch | Passed; same 11 encounters | [Screenshot](images/phase-h-standalone.png) |
| Inferno SMART STU2.2 public client protocol suite | PASS with test-environment caveat below | [Session](https://inferno.healthit.gov/suites/smart_client_stu2_2/cmC7qGYwGwl), [report screenshot](images/phase-h-inferno.png) |

The sandbox patient was synthetic `39e53c6f-309b-4ed7-b650-79a4ef7b1e99`.
Its unrelated clinical visits display as Unknown/No result rather than being
invented dengue/malaria results. Public sandbox data can reset.

## Inferno run details and limitation

Inferno Test Kit 1.0.2 / core 1.4.0, SMART App Launch STU2.2 Client,
**SMART App Launch Public Client**, standalone flow, reported **PASS** at
14:42 Asia/Dhaka on 2026-09-09:

- 3.01 Verify SMART App Launch Public Client Registration
- 5.03 Access a secured FHIR endpoint using SMART App Launch
- 5.07 Verify SMART App Launch Authorization Requests
- 5.10 Verify SMART Token Requests
- 5.12 Verify SMART Token Use (two FHIR requests)

Registered client: `prohori-dashboard`. Redirect:
`https://localhost:5443/`; launch URL: `https://localhost:5443/launch.html`.
The production Vite build was served over loopback HTTPS with a temporary
self-signed certificate. Launch context was
`{"patient":"prohori-inferno-demo"}`, with a synthetic Patient resource and an
empty search Bundle configured as the simulator's responses.

**This is a protocol-suite pass, not an unqualified browser interoperability or
certification claim.** The hosted Inferno FHIR endpoint's OPTIONS responses
lacked `Access-Control-Allow-Origin`, so a normal browser blocked the Patient
read before transmitting the authenticated GET. The initial normal-browser
run therefore failed token-use verification. The successful diagnostic run
used a fresh, isolated Playwright Chromium process with
`--disable-web-security` and accepted the temporary loopback certificate.
Requests still went to the real Inferno service; the authorization, PKCE,
token exchange, and bearer-token requests were not mocked or rewritten.

No browser security override is present in the application, committed
Playwright configuration, CI, or normal demo instructions. Both real sandbox
launches and the Keycloak end-to-end test passed with normal browser security.
A strict normal-browser Inferno rerun remains dependent on fixing the hosted
simulator's CORS response (or running a correctly configured local Inferno).
The linked session is ephemeral; the screenshot preserves the reported result.

## Reproduce local end-to-end verification

Follow [local setup](smart-launch.md#local-keycloak-demo), then:

```sh
cd web
npx playwright install chromium
PROHORI_LIVE_SMART=1 npm test -- --workers=1
```

The live test uses only the fixed synthetic local patient; it toggles `active`,
checks the stale ETag, and restores the preceding value through JSON Patch.
The live test is skipped in ordinary CI, where HAPI/Keycloak are not running.
The three deterministic SMART browser tests run in CI.

Lint has two pre-existing warnings in SummaryTiles/Filters. Vite emits a
CommonJS interoperability warning from fhirclient's transitive WebCrypto shim;
actual PKCE execution was verified in Chromium. No secrets or bearer tokens
are stored in these evidence files.
