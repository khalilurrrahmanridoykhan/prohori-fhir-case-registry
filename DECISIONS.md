# Decisions

## 2026-09-13 — Phase L (HL7 v2 → FHIR)

- **`Prohori.V2Gateway` is its own project and its own test project**, not folded into
  `Prohori.Api`. A bare top-level `Program.cs` produces a global `Program` class regardless of
  `RootNamespace`; referencing two such projects from one test project makes `Program` ambiguous
  (hit this immediately trying to add V2Gateway tests to `Prohori.Api.Tests` — `CS0433`).
  `tests/Prohori.V2Gateway.Tests/` avoids it entirely and mirrors "V2Gateway is its own
  deployable service" architecturally, not just as a workaround.
- **NHapi's `Terser`**, not the version-specific typed message classes NHapi also ships — one
  code path handles ADT^A01/A08/A03 (and any v2 version) via string field paths (`/PID-3-1`)
  instead of a typed class per event/version combination.
- **The StructureMap is authored (real FHIR Mapping Language, `ig/input/maps/hl7v2-to-fhir.map`)
  but not executed via a live `$transform`.** Checked local HAPI v8.0.0's own CapabilityStatement
  first — it advertises no `transform` operation for StructureMap. matchbox (the plan's named
  alternative) publishes to a registry unreachable within scope; standing up an unfamiliar Java
  service on faith wasn't the right trade. The `.map` is the reviewable declaration of intent;
  `V2ToFhirMapper.cs` is the executable side, hand-kept in sync (a segment→field table in
  docs/hl7v2-to-fhir.md is the sync check) — the same relationship every other phase's
  declarative artifact has had to its executable counterpart.
- **Idempotency follows the trigger event, not one blanket rule**: `A01` conditionally *creates*
  Patient + Encounter (`If-None-Exist`); `A08`/`A03` conditionally *update* both instead
  (`PUT ?identifier=...`) — a retried admit must not duplicate, but a demographic correction or a
  discharge must actually land on the existing record. Verified live: an A08 with a changed
  address lands on the same Patient id; an A03 finishes the same Encounter with `period.end` set.
- **`/adt` has no bearer-token auth**, unlike every other write path since Phase H. A real ADT
  feed arrives over MLLP from a private hospital network segment, not a browser or public API
  caller — bearer-token auth doesn't model that trust boundary, and reusing Phase H's SMART
  machinery here would duplicate it without teaching anything new. Documented, not silent.
- **MLLP itself is not implemented** — `/adt` takes plain HTTP POST, far easier to test and demo
  at the cost of not being the real wire protocol. An MLLP listener would sit in front of the same
  `V2Parser`/`V2ToFhirMapper` pair unchanged.
- Preserve implementation history with a merge commit and tag `phase-l`, same ritual as every
  prior phase.

## 2026-09-12 — Phase K (Terminology)

- Author CodeSystem/ValueSet/ConceptMap in **FSH**, unlike Phase J's Questionnaire.
  `validator_cli` needs them as files (via `-ig`) to check `ProhoriObservation`'s
  bindings, and `scripts/load-terminology.sh` needs the identical JSON to PUT
  onto a live server; `sushi . --snapshot` produces one file both consumers use
  directly, no export step. SUSHI 3.20's FSH grammar has no `ConceptMap:`
  keyword — authored as a definitional `Instance: ... InstanceOf: ConceptMap`
  instead. See docs/terminology.md.
- **Finding:** the offline validator (`-tx n/a`) can't actually enforce a
  `required` binding to an external code system it has no local definition
  for (SNOMED CT, LOINC — both licensed, neither ships with the free HL7
  packages): it emits a warning ("code cannot be validated") and passes
  regardless. An out-of-ValueSet SNOMED code sailed through with
  `Success: 0 errors` in testing. Dropping `-tx n/a` to force a real check
  instead tries every terminology lookup against `tx.fhir.org` over the
  network — **hung past 90 seconds** for a single resource in testing, not
  viable in CI. Kept `-tx n/a` in `validate-ig.sh` (offline, structural checks
  only for `ProhoriObservation`); binding enforcement is proved live against
  local HAPI instead (`scripts/verify-terminology.sh`), which turns out to be
  the more honest demonstration anyway — a production deployment validates
  against a live server, not a static CLI.
- Chose enumerated (`compose.include[].concept[]`) ValueSets throughout —
  including `bd-condition-icd11-diagnosis-valueset-fixed`, Prohori's own
  authored fix for BD-Core-FHIR-IG 0.4.6's empty ICD-11 ValueSet (Phase F
  finding) — over filter/hierarchy-based composition. A closed, small list a
  live server can `$expand`/`$validate-code` with no external terminology
  loaded, matching what `bd-condition` should have shipped. Not filed upstream
  automatically — that's a real action against a government repository under
  someone else's ownership; a draft issue is ready in docs/terminology.md.
- `$translate` demonstrated with real narrative purpose, not a bare demo
  endpoint: `prohori-rdt-result-legacy-to-snomed` maps a legacy ODK export's
  plain `pos`/`neg` codes to SNOMED CT, feeding a third way into the same case
  Bundle (`POST /legacy-import/cases`, alongside the typed API and the SDC
  form) — `TerminologyClient` calls `$translate`, then hands off to the same
  `CaseBundleBuilder`/`FhirCaseService` every other path uses.
- Preserve implementation history with a merge commit and tag `phase-k`, same
  ritual as every prior phase.

## 2026-09-11 — Phase J (Questionnaire & SDC)

- Build the `Questionnaire` in C# (`QuestionnaireCatalog.cs`), not FSH. Every other
  Prohori profile constrains instances a server receives; a Questionnaire's returned
  resource *is* the artifact, so a separate FSH definition would just be a second place
  for a linkId to drift from the extraction code that reads it back. Exported for CI via
  `--export-questionnaire` (mirrors `--export-bd-core`) and validated against base FHIR
  R4 only — no custom IG, nothing is being constrained. See `docs/sdc.md`.
- `QuestionnaireLinkIds` (C#) / `LINK` (`web/src/questionnaire/linkIds.ts`) are hand-kept
  string mirrors, same relationship `terminology.ts` already has with `Systems.cs`. A
  small, stable id set; `QuestionnaireCatalogTests` and `QuestionnaireExtractionTests`
  independently pin the shape from both ends so drift fails a test, not silently.
- `$extract` never re-implements Bundle construction: it maps a `QuestionnaireResponse`
  into the same `CaseSubmission` the typed `/cases` endpoint takes, then hands off to the
  existing `CaseBundleBuilder` + `FhirCaseService.SubmitAsync(Bundle)`. One Bundle-shape,
  proven live in Phase C's integration tests, carries over transitively.
- `$populate`'s request body is a bare `{"nationalId": "..."}`, not a formal `Parameters`
  resource. A deliberate simplification for a single lookup field — documented, not
  silent, same as the BD-Core `bd-condition` workaround (Phase F).
- `disease`/`rdtResult` `answerOption`s carry the identical SNOMED codes
  `CaseBundleBuilder` already writes onto the Observation, so extraction is a lookup, not
  a translation — real terminology translation (`ConceptMap`/`$translate`) is Phase K.
- The dashboard's New Case form (`QuestionnaireForm.tsx`) renders `Questionnaire.item[]`
  generically by `item.type` (group/string/choice/date/dateTime), evaluating `enableWhen`
  client-side — nothing about which fields exist or when `diagnosisNote` shows is
  hardcoded in the renderer. `$populate`/`$extract` require the same `CaseWrite`
  bearer-token scope as `/cases` (Phase H); no token shows a SMART-launch prompt rather
  than a broken form.
- Preserve implementation history with a merge commit and tag `phase-j`, same ritual as
  every prior phase.

## 2026-09-10 — Phase I

- Keep HAPI's real Bulk Data batch engine behind an opt-in authenticated API
  gateway. Keycloak registers the backend public JWKS by value and validates
  RS384 `private_key_jwt`; it does not turn HAPI into a native SMART server.
- Use an isolated HAPI 8.0.0 / Keycloak 26.6.0 Compose project on loopback
  8090/8091, with the API on 5280. Generate RSA3072 keys locally; ignore keys,
  generated realm configuration and export files. No shared private credential.
- Match the plan's `system/*.read` scope and advertise permission-v1. Keep bulk
  disabled in the deployed application until a production auth/storage setup
  is configured; do not conflate a local backend lab with the public dashboard.
- Bind polling and downloads to the access-token subject. Validate upstream
  polling/Binary URLs, rewrite manifest links through the gateway, and disable
  redirects. The bounded in-memory job registry is explicitly single-instance.
- Choose aggregate JSON instead of Postgres for the small synthetic cohort.
  One Encounter counts as a field case; RDT positivity uses known positive and
  negative observations. Full exports replace state; deltas upsert by type/id.
  Commit the server watermark last and reject gaps or changed export scope.
- Fail on error/deletion manifests. Cohort removals, filter changes, and servers
  without deletion feeds require full refreshes; this is not a general warehouse.
- Gate CI on the actual two-visit full export and a one-observation delta using
  Docker services. Keep this separate from the non-blocking public sandbox tests.
  Preserve each implementation commit with a merge commit and tag `phase-i`.
- Live HAPI 8 Group `_since` exported the changed Observation plus its unchanged
  Encounter reference. Enforce `meta.lastUpdated > _since` while streaming the
  gateway's NDJSON downloads, retaining old references in the full local snapshot.
  Fail malformed delta rows and require a full refresh for membership changes.

## 2026-09-09 — Phase H

- Use fhirclient for SMART authorization-code + S256 PKCE, state and token
  exchange. Resolve and read the launch patient before mounting React. Keep the
  public synthetic cohort demo available when no SMART session exists.
- Require issuer/JWKS signature, audience and lifetime validation for case
  writes. Match `user/*.write` as a whole scope, protect BD-Core writes too,
  and generate CI validation artifacts offline instead of bypassing auth.
- Keycloak 26.6 provides the local IdP; it is not a SMART server. An explicitly
  enabled context/read facade uses the signed patient claim for the fixed
  synthetic demo. Do not advertise unsupported SMART server capabilities.
- Use a scoped Firely client to avoid sharing mutable SDK request state across
  concurrent requests. Patient PATCH/PUT transport forwards conditional writes
  atomically to HAPI and requires an explicit ETag.
- Translate only HAPI's specific stale PATCH error (`409`, `HAPI-0974`) to
  `412`; derive omitted PATCH ETags from versioned Content-Location. Live tests
  discovered both HAPI behaviors. Preserve unrelated upstream conflicts.
- Synchronous Patient writes may ignore respond-async. Real async jobs remain
  in Phase I; do not manufacture successful 202/polling flows.
- The H–P plan supersedes the old squash-merge note: retain granular commits,
  merge the phase PR with a merge commit, then tag `phase-h`.


One dated line per non-obvious choice. Newest at the top.

## 2026-09-03 — Phase G (ship it live)

- **Dashboard → Vercel**, **API → Render** (Docker), **FHIR store → DGHS sandbox**
  — all free tiers. `web/.env.production` and `deploy/render.yaml` bake in the
  sandbox URL; no dashboard config needed.
- **`deploy/Dockerfile`** — multi-stage .NET 8, runs as the image's `app` user on
  `:8080`. `.dockerignore` keeps host `bin/obj` out (they clobber the container
  restore). `global.json` relaxed to `rollForward: latestMinor`.
- **`keepalive.yml`** — 13-min cron ping so Render's free service doesn't sleep;
  no-ops until the `RENDER_API_URL` repo variable is set.
- **Vercel + Render both need a one-time dashboard connect** (GitHub auth) — the
  Vercel MCP deploy is `403 forbidden` for this hobby-team project. Steps in the
  README "Deploy" section.
- `seed-cohort.py` seeds the DGHS sandbox one transaction per patient.

## 2026-09-03 — Phase F (BD-Core conformance + live submission)

- **`BdCoreBundleBuilder`** + `POST /bd-core/cases` (`?dryRun=true` returns the
  Bundle). Consumes the published `bd.fhir.core#0.4.6` profiles — Prohori doesn't
  re-profile.
- **Result:** validator_cli **0 errors** against `bd.fhir.core`, and the **live
  DGHS sandbox accepted** the 5-resource Bundle (IDs in
  `docs/bd-core-submission.md`).
- **5 resources, not 6** — no separate `Condition`. `bd-condition` v0.4.6 binds
  `Condition.code` (`required`) to `bd-condition-icd11-diagnosis-valueset`, which
  ships with an **empty `compose`** — unsatisfiable. Reported as a finding;
  diagnosis recorded on `Encounter.reasonCode` (ICD-11 MMS, unconstrained).
- **`bd-patient` quirk:** `identifier:NID.type.text` is pattern-pinned to the
  literal `"Organization identifier"` (an IG copy-paste artefact). Matched for
  conformance, with a code comment.
- `If-None-Exist` on facility code / HRIS code / NID → Organization, Practitioner
  and Patient are reused across submissions.
- `#nullable disable` on `BdCoreBundleBuilder.cs` (Firely model, same rationale
  as the test project).
- Optional add-ons (GIS / ODK / DHIS2) deferred — primary goal met.

## 2026-09-03 — Phase E (own server + profile)

- **Local server:** `deploy/docker-compose.yml` — `hapiproject/hapi:v8.0.0` with
  **embedded H2** (persisted to a volume), config layered via
  `SPRING_CONFIG_ADDITIONAL_LOCATION` (only CORS + `validation.requests_enabled`).
  Dropped Postgres: HAPI v8's bundled Flyway rejects Postgres 15 **and** 16
  (`Unsupported Database`), and disabling Flyway trips a bean-init cycle. Container
  runs as `root` to write the H2 file on the volume. On macOS, **Colima**
  provides the daemon (no Docker Desktop / GUI licence).
- **Verified 2026-09-03:** local HAPI rejects a Patient with no NID / wrong
  identifier system / non-digit NID (`422` + profile `OperationOutcome`); accepts
  the conformant one; `Prohori.Api` runs against it with only an env-var change.
- **Profile authored in FSH** (`ig/input/fsh/ProhoriPatient.fsh`), built with
  **SUSHI** (`FSHOnly: true` — no IG website, just the FHIR artifacts).
  `fsh-generated/` stays **gitignored** (generated); CI regenerates it in the
  `IG` job, and `validate-ig.sh` / `load-profile.sh` tell you to run SUSHI first.
- **ProhoriPatient** requires: a National-ID identifier (sliced by `system`,
  fixed to `http://health.gov.bd/sid`, `value` matches `^[0-9]{10,17}$` via
  invariant), `name` + `name.family`, `gender`, `birthDate`.
- **Enforcement, two layers:** (1) HAPI validates writes against `meta.profile`
  after `scripts/load-profile.sh` pushes the SD; (2) `scripts/validate-ig.sh`
  runs HL7's `validator_cli.jar` against `ig/input/tests/` fixtures in CI.
- **Chose the official validator CLI over `Hl7.Fhir.Specification`** (the plan's
  original call). Firely SDK 6.x dropped the in-process `Validator`;
  `Firely.Fhir.Validation` 3.x gates snapshot generation behind an Enterprise
  licence. `validator_cli.jar` is HL7's reference impl, needed for Phase F
  anyway, runs on Java 11+.
- Shell gotcha: `grep -q` + `set -o pipefail` gives a false failure when the
  upstream process is still writing (SIGPIPE). `validate-ig.sh` uses the
  validator's exit code instead.

## 2026-09-03 — Phase D (React dashboard)

- **React 19 + Vite + TS** via `create-vite` (v9 template — ships `oxlint`). Deps:
  `react-router-dom` 7, `@tanstack/react-query` 5, `@types/fhir` (dev).
- **No FHIR SDK in the browser** — plain `fetch` + `@types/fhir` types. This
  `@types/fhir` (0.0.44) is ESM (`export interface Patient`), not a global
  `fhir4` namespace — so resource types are imported via a barrel,
  `src/fhir/r4.ts` (`export type { Patient } from "fhir/r4"`).
- **One query for the case list**: `Encounter` anchored, `_include` the Patient,
  `_revinclude` the Observation + Condition — one round trip, grouped client-side
  into one row per visit. Every case has exactly one Encounter, so Encounter is
  the right anchor (Observation-anchored would miss nothing but Condition-anchored
  would drop the negatives).
- **Filters are client-side** over the loaded set — fine for a demo cohort;
  noted that server-side params are the scale answer.
- **Timeline** = `Patient/{id}/$everything`, sorted by date. `recordedDate` on a
  Condition is date-only so it can sort before the visit — a real data nuance,
  left as-is.
- Dashboard reads the FHIR server **directly**, not through `Prohori.Api` (the
  API is write-only so far). HAPI's public server sends `Access-Control-Allow-Origin: *`
  so browser calls work with no proxy.
- Theming: `:root` = light palette, `@media (prefers-color-scheme: dark)` swaps
  tokens. No theme toggle, so the artifact-skill's 3-state pattern isn't needed.
- Added `scripts/reset-cohort.sh` — delete all `_tag`-matched resources
  (Condition→Observation→Encounter→Patient order) for a clean re-seed.

## 2026-09-02 — Phase C (.NET 8 + Firely write client)

- **Firely `Hl7.Fhir.R4` 6.4.0** (not the 5.x the plan assumed — 6.x is current).
  `FhirClient` registered as a singleton (wraps `HttpClient`, meant to be reused).
- **Dropped FluentAssertions** — v8 moved to a paid commercial licence. Using
  **Shouldly** (free, BSD) instead. On-brand given this project's whole framing is
  tool governance.
- **`CaseBundleBuilder` is a pure function** (`CaseSubmission` → `Bundle`), no I/O,
  so the mapping logic is unit-tested without a server (19 unit tests).
- **Conditional create** via `Bundle.entry.request.ifNoneExist` on the Patient's
  National ID — a second visit for the same person reuses the existing Patient
  instead of duplicating it. Verified by an integration test.
- **`OperationOutcomeMapper`**: every FHIR error → one RFC 7807 `ProblemDetails`
  with the raw issues under an `issues` extension. Callers handle one error shape.
- **Validation**: `MiniValidation` (recurses into the nested `PatientInput`);
  minimal APIs don't auto-validate DataAnnotations.
- **Integration tests** tagged `[Trait("Category", "Integration")]`; CI gate runs
  `--filter "Category!=Integration"`, a separate non-blocking job runs them against
  hapi.fhir.org (sandbox flakiness shouldn't fail the gate).
- HAPI-2840 again: two identical Encounters in back-to-back submissions are
  rejected, so the "no duplicate patient" test submits the second visit on a
  different date — which is realistic anyway.
- `.NET 8` via Homebrew `dotnet@8` (keg-only): needs
  `export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"` +
  `PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"`.

## 2026-09-02 — Phase B (Search)

- **Seed with a transaction Bundle** (`scripts/seed-cohort.py`, stdlib only): one
  POST creates 8 patients + encounters + observations + conditions with
  `urn:uuid:` refs. Building the Bundle properly is Phase C's job in .NET; here
  it's just a means to get searchable data in.
- **8-patient cohort** deliberately varied on sex / birth-decade / city / disease
  / result so one small dataset exercises every search feature.
- **Every query `_tag`-scoped** to `urn:prohori|demo-cohort` — the public server
  is full of other people's data.
- **Findings** (`docs/search-queries.md`): `name:exact` matches individual name
  parts, not "given family" — use `family:exact`. HAPI ignores a `:missing`
  modifier inside a `_has` chain. `$everything` returns the full 4-resource case
  in one call — that's what the Phase D dashboard will use.
- `_has` (reverse chaining) is the key idiom for "patients with a positive
  result / a dengue diagnosis" — the patient resource carries none of that.

## 2026-09-02 — Phase A

- **Repo name `prohori-fhir-case-registry`.** `prohori` (প্রহরী, "sentinel") as a
  product name; `-fhir-case-registry` added for context so the slug says what it
  is. Not "learning" / "tutorial" — this is portfolio-facing.
- **License Apache-2.0** (not MIT) — explicit patent grant, standard for
  health-IT / FHIR tooling.
- **Backend: .NET 8 LTS + Firely SDK** (`Hl7.Fhir.R4`). Firely is the
  reference-grade FHIR SDK (built by spec co-authors); .NET 8 is LTS to Nov 2026.
  Chosen over Node/Python as a deliberate career bet on the Azure health-data
  market. Language friction accepted.
- **API explorer: Bruno**, collection committed as plain `.bru` files under
  `bruno/`. Git-friendly, no account, no cloud sync. `scripts/phase-a.sh` mirrors
  it in curl for zero-install reproducibility.
- **FHIR server path:** public HAPI (`hapi.fhir.org/baseR4`) for Phases A–D →
  self-hosted HAPI in Docker for Phase E → BD-Core sandbox
  (`sandbox.fhir.dghs.gov.bd/fhir`) for Phase F and the live demo.
- **`meta.tag = urn:prohori|demo-cohort`** on every resource, so our data is
  findable and removable on the shared public server.
- **Observed:** HAPI public server enforces `HAPI-2840` (no duplicate resources)
  and resets periodically — scripts generate unique identifiers per run; never
  post real data.
- **Git workflow:** one branch + PR + `phase-x` tag per phase; whole phase lands
  on `main` as one squash-merged unit; `main` always demoable. Conventional
  commits. No `Co-Authored-By` trailers.

## Deployment (planned, Phase G)

- Frontend → **Vercel** (`khalilurrahmanridoykhan`), free.
- `.NET` API → **Render** free web service (Vercel has no .NET runtime; ~50s
  cold start mitigated by a cron keepalive).
- FHIR store → **BD-Core sandbox** (no database to host).
