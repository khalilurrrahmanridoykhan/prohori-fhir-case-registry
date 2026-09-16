# FHIR competency matrix

Every phase, the exact job competency it evidences, and the PR that proves
it. Built for interview prep and CV backing — every claim below has a real
PR and a real, live-verified artifact behind it, not a tutorial exercise.

| Phase | Competency evidenced | PR | Evidence |
| :--- | :--- | :--- | :--- |
| A — REST by hand | RESTful CRUD, `_history`/vread, `OperationOutcome` semantics against a real server | [#1](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/1) | `docs/phase-a-notes.md`, Bruno collection |
| B — Search | Every FHIR search param type, `_include`/`_revinclude`, `_has`, chaining, paging | [#4](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/4) | `docs/search-queries.md` (27 documented queries) |
| C — Write client | Transaction Bundles, `If-None-Exist` conditional create, `OperationOutcome` → RFC 7807 mapping, in a real backend language (.NET 8 + Firely SDK) | [#5](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/5) | `src/Prohori.Api/Fhir/CaseBundleBuilder.cs` |
| D — Read client | Consuming FHIR search/`$everything` from a real frontend, without a backend-shaped API in between | [#6](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/6) | `web/` dashboard |
| E — Own server | Self-hosted FHIR JPA server, FSH/SUSHI profiling, server-side request validation | [#10](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/10) | `ig/`, `deploy/docker-compose.yml` |
| F — National IG conformance | Conforming to a real government Implementation Guide, submitting to its real sandbox, finding and documenting one of its actual defects | [#11](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/11) | `docs/bd-core-submission.md` |
| G — Ship it | Free-tier production deployment of a FHIR-backed frontend | [#13](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/13) | Live at prohori-fhir-case-registry.vercel.app |
| H — SMART on FHIR | SMART App Launch 2.0 (standalone + EHR), PKCE, OAuth2/OIDC scopes, JSON Patch/FHIRPath Patch, `If-Match` optimistic concurrency | [#16](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/16) | `docs/phase-h-verification.md` — Inferno PASS |
| I — Backend Services + Bulk Data | SMART Backend Services (RS384 client-assertion), async Bulk Data `$export`, NDJSON, incremental `_since` | [#17](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/17) | `docs/bulk-export.md` |
| J — SDC | `Questionnaire`/`QuestionnaireResponse`, `$populate`/`$extract`, mapping a real ODK-style intake form to FHIR | [#19](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/19) | `docs/sdc.md` |
| K — Terminology | `$expand`/`$validate-code`/`$translate`/`$lookup` against a live server; authoring a ValueSet a real national IG ships broken | [#21](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/21) | `docs/terminology.md` |
| L — HL7 v2 integration | HL7 v2 message parsing (NHapi), FHIR Mapping Language, honest reporting of what actually executes vs. what's declared | [#22](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/22) | `docs/hl7v2-to-fhir.md` |
| M — CQL & Measures | Clinical Quality Language, `Library`/`Measure`, computable population logic, `MeasureReport` | [#24](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/24) | `docs/measures.md` |
| N — IG Publisher | Running the real HL7 IG Publisher, `CapabilityStatement` authoring, publishing to GitHub Pages, catching a real code-correctness defect via live terminology validation | [#25](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/25) | `docs/publishing-the-ig.md`, live IG site |
| O — Real-time, provenance, consent | R4 rest-hook Subscriptions, `AuditEvent`, `Consent`-based access control with a properly-audited break-glass override | [#26](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/pull/26) | `docs/realtime-provenance-consent.md` |
| P — Consolidation | Turning 15 phases of real, verified work into an interview-ready narrative | — | This file + [`architecture-case-study.md`](architecture-case-study.md); the HL7 FHIR Proficiency Exam itself is a separate, not-yet-taken step |

## Reading this for an interview

Each PR is small enough to actually walk through live. The findings worth
leading with, in rough order of how often they come up: the BD-Core
empty-ValueSet defect (F/K — real government IG, real defect, real
workaround); the malaria code-correctness bug live terminology validation
caught (N — nothing before that phase ever checked a coded element's
*value*); and the HAPI transaction-Bundle / rest-hook delivery limitations
found by isolating raw HTTP behavior rather than trusting documentation (O).
Each is a concrete story with a before/after, not a résumé bullet.
