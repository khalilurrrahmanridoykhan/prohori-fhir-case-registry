# Data provenance

| Source | Use and transformation | Access / licensing |
| --- | --- | --- |
| Repository fixtures and `scripts/seed-cohort.py` | Authored synthetic inputs become linked FHIR transaction resources through the Firely builders | Repository-authored code and fixtures: Apache-2.0; no real participant data |
| `scripts/seed-smart.py` | Creates one fixed synthetic Patient, Encounter and Observation on local HAPI for authentication tests | Authored for Phase H, 2026-09-09; Apache-2.0 |
| SMART Health IT sandbox (`launch.smarthealthit.org`, `r4.smarthealthit.org`) | Read-only synthetic patient-context demonstration; no clinical inference or population estimate | Accessed 2026-09-09; external service/data retain their upstream terms, not relicensed by this repository |
| DGHS BD-Core FHIR IG / sandbox | National profile definitions used to validate and submit authored synthetic bundles | See [submission record](docs/bd-core-submission.md); external IG and terminology retain upstream licensing |
| Inferno SMART test service | Synthetic Patient and empty Bundle supplied to its server simulator to test OAuth requests | Accessed 2026-09-09; see [verification details](docs/phase-h-verification.md) |

No patient records were collected from a hospital, survey participant or health
worker for this project. Screenshots of SMART sandbox patients are synthetic.
The dashboard groups Encounter-linked Observations and Conditions by patient;
its counts describe retrieved demo resources, not disease prevalence.
