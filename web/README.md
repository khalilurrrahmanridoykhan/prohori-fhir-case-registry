# Prohori dashboard (Phase D)

A read-only surveillance dashboard over the FHIR server: case list, filters,
summary tiles, and a per-patient timeline.

React 19 · Vite · TypeScript · TanStack Query · React Router. No FHIR SDK — it
talks to the server with `fetch` and types resources with `@types/fhir`.

## Run

```bash
cp .env.example .env          # first time
npm install
npm run dev                   # http://localhost:5173
```

Seed data first if the server is empty:

```bash
cd .. && python3 scripts/seed-cohort.py      # 8-case cohort
# clean slate: bash scripts/reset-cohort.sh && python3 scripts/seed-cohort.py
```

## Configuration

| Env var | Default | Purpose |
| :--- | :--- | :--- |
| `VITE_FHIR_BASE` | `https://hapi.fhir.org/baseR4` | FHIR R4 server the dashboard reads |
| `VITE_API_BASE` | `http://localhost:5279` | Prohori.Api (Phase C) — not used by the read views yet |

## How it reads the data

| View | FHIR call |
| :--- | :--- |
| Case list | `GET /Encounter?_tag=urn:prohori\|demo-cohort&_include=Encounter:subject&_revinclude=Observation:encounter&_revinclude=Condition:encounter&_sort=-date` — one request, then grouped client-side into one row per visit |
| Filters | client-side over the loaded set (disease / result / city / date). Server-side filtering is the scale answer; fine for a demo cohort |
| Case timeline | `GET /Patient/{id}/$everything` → sorted chronologically |

## Build

```bash
npm run build     # tsc -b && vite build  -> dist/
npm run preview
```

## Layout

```
src/
  config.ts            env + the cohort tag
  fhir/
    client.ts          fetch wrapper, reference-id parsing
    r4.ts              the @types/fhir resource types this app uses
    terminology.ts     LOINC/SNOMED code -> label maps
    cases.ts           useCases() + useCaseTimeline() query hooks
  components/           Layout, Pills, SummaryTiles, Filters, CaseTable
  pages/               Dashboard, CaseDetail
  styles.css           design tokens (light + dark) + layout
```
