# Decisions

One dated line per non-obvious choice. Newest at the top.

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
