# Decisions

One dated line per non-obvious choice. Newest at the top.

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
