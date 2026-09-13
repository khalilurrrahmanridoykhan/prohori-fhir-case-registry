### Build phases

One branch, one pull request, one `phase-*` git tag per phase — `main` always
demoable. Full detail: [DECISIONS.md](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/blob/main/DECISIONS.md)
and `docs/phase-*` / `docs/*.md`.

| Phase | What it proved | Tag |
| :--- | :--- | :--- |
| A | The FHIR REST API by hand — CRUD, `_history`, `OperationOutcome` | `phase-a` |
| B | Search — every parameter type, `_include`/`_revinclude`, `_has`, paging | `phase-b` |
| C | `.NET 8` + Firely write client — transaction Bundle, conditional create | `phase-c` |
| D | React dashboard — case list, filters, patient timeline | `phase-d` |
| E | Self-hosted HAPI + a `ProhoriPatient` profile, server-side validation on | `phase-e` |
| F | BD-Core-FHIR-IG conformance + a Bundle accepted by the live DGHS sandbox | `phase-f` |
| G | Shipped live — Vercel, seed data | `phase-g` |
| H | SMART App Launch 2.0, Keycloak, protected writes, PATCH/`If-Match` | `phase-h` |
| I | SMART Backend Services + real Bulk Data `$export` | `phase-i` |
| J | Questionnaire / SDC — `$populate`/`$extract` into the same case Bundle | `phase-j` |
| K | Terminology — `$expand`/`$validate-code`/`$translate`; fixed BD-Core's empty ICD-11 ValueSet | `phase-k` |
| L | HL7 v2 → FHIR — `Prohori.V2Gateway`, admit/update/discharge idempotency | `phase-l` |
| M | CQL + `Measure`/`MeasureReport`, evaluated server-side | `phase-m` |
| N | **This Implementation Guide** — published, with a CapabilityStatement | `phase-n` |

A recurring thread from Phase K onward: before building around an operation
(`$transform`, `$evaluate-measure`, a live CQL engine), check whether it's
actually available in this environment. Three times it wasn't — each is
documented as a finding, not glossed over, in the relevant `docs/*.md` and in
`DECISIONS.md`.

#### What's still ahead

| Phase | Goal |
| :--- | :--- |
| O | Subscriptions (rest-hook / topic-based), Provenance/AuditEvent, Consent |
| P | Consolidation — HL7 FHIR Proficiency Exam, portfolio, competency matrix |

Full plan: [`Prohori — FHIR Mastery (Phases H–P) — Plan.md`](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry)
in the author's planning notes (not part of this repo).
