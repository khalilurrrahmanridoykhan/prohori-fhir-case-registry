# Prohori — FHIR API collection

Requests for learning and probing the FHIR REST API by hand (Phases A–B).

| Folder | Phase | What |
| :--- | :--- | :--- |
| `01`–`08` (root) | A | CRUD, history, delete, forced `OperationOutcome` |
| `search/` | B | token / string / date / composite params, `_revinclude`, `_has`, `$everything` |

Seed the cohort before running `search/`:  `python3 ../scripts/seed-cohort.py`

## Run in the Bruno desktop app

1. Open **Bruno** → *Open Collection* → select this `bruno/` folder.
2. Top-right environment dropdown → **hapi-sandbox**.
3. Open requests **01 → 08** and send them **in order**.
   - `02 Create Patient` sets `runId` and `patientId` as runtime variables.
   - `03`–`07` reuse `patientId`; `04` reuses `runId`.
   - Runtime variables reset when you close the collection — just re-run `02`.

## Run headless (no GUI)

```bash
npm i -g @usebruno/cli
cd bruno
bru run .      --env hapi-sandbox      # root: Phase A (01–08)
bru run search --env hapi-sandbox      # Phase B search catalogue
```

## Run with plain curl

```bash
bash ../scripts/phase-a.sh
```

## Environments

| Env | baseUrl |
| :--- | :--- |
| `hapi-sandbox` | `https://hapi.fhir.org/baseR4` (shared, wiped periodically — never real data) |

Add `environments/local-hapi.bru` with `baseUrl: http://localhost:8080/fhir` once
the Phase E Docker server is up.

## Notes

- Every resource carries `meta.tag = urn:prohori|demo-cohort` — find your data
  with `GET /Patient?_tag=urn:prohori|demo-cohort`.
- The public HAPI server rejects byte-duplicate resources (`HAPI-2840`); request
  `02` generates a unique National ID per run to avoid it.
