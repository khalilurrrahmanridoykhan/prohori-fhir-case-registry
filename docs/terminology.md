# Terminology services & operations (Phase K)

Prohori stops treating disease/result/diagnosis codes as strings it happens to
write consistently, and starts treating them as **bindings** a server can
enforce and a client can ask questions of — `$expand`, `$validate-code`,
`$translate` — against a real terminology server.

## The artifacts

Authored in FHIR Shorthand, `ig/input/fsh/ProhoriTerminology.fsh` (idiomatic
here, unlike Phase J's Questionnaire — see "Why FSH, unlike the Questionnaire"
below):

| Resource | Id | What it's for |
| :--- | :--- | :--- |
| `CodeSystem` | `prohori-rdt-result-legacy` | The plain `pos`/`neg` codes a legacy ODK/KoBo export uses, before anything is SNOMED-coded. |
| `ValueSet` | `prohori-rdt-result-valueset` | SNOMED CT `10828004`/`260385009` — binds `ProhoriObservation.valueCodeableConcept`. |
| `ValueSet` | `prohori-rdt-test-valueset` | LOINC `42239-4`/`70048-1` — binds `ProhoriObservation.code`. |
| `ValueSet` | `bd-condition-icd11-diagnosis-valueset-fixed` | ICD-11 MMS `1D40`/`1F4Z` — the fix for BD-Core's empty ValueSet, see below. |
| `ConceptMap` | `prohori-rdt-result-legacy-to-snomed` | `pos → 10828004`, `neg → 260385009`. Called via `$translate`. |

All four are **enumerated** (`compose.include[].concept[]` / `group.element[]`)
rather than filter- or hierarchy-based — a closed, small list, not a
subscription to an external terminology's full structure. That choice matters
for what follows.

## Binding strengths

`ProhoriObservation` (new this phase) required-binds two elements:

```
* code from ProhoriRdtTestValueSet (required)
* valueCodeableConcept from ProhoriRdtResultValueSet (required)
```

`required` is the strictest of FHIR's four binding strengths (`required` >
`extensible` > `preferred` > `example`) — a conformant instance's code **must**
be in the ValueSet, full stop. `ProhoriPatient` (Phase E) has no coded elements
strict enough to need this; `ProhoriObservation` is where Prohori's own IG
first exercises it.

## Finding: the offline validator can't actually enforce these

The obvious plan — validate `ProhoriObservation` examples with `validator_cli`
and drop `-tx n/a` "now that terminology checks are on" — doesn't work:

- **Without `-tx n/a`**, the validator tries to reach `tx.fhir.org` for every
  terminology check, including ones our own enumerated ValueSets could resolve
  without it. In testing this **hung past 90 seconds** for a single resource —
  not viable in CI.
- **With `-tx n/a`** (offline), the validator can't load SNOMED CT or LOINC
  locally (both are licensed; neither ships with the free HL7 packages), so it
  can't check ValueSet membership for either binding at all — it emits a
  *warning* ("the code cannot be validated") and **passes anyway**, silently
  not doing the check. An out-of-ValueSet SNOMED code (`260415000`, "Not
  detected") sailed through with `Success: 0 errors` in testing — that's not
  the binding working, that's the binding being skipped.

So `scripts/validate-ig.sh` keeps `-tx n/a` (fast, deterministic, offline) and
only proves what it honestly can offline: `ProhoriObservation`'s *structural*
rules (a conformant example passes; one missing the required `subject` fails).
The terminology bindings are proved for real instead, against a live server —
which turns out to be where they belong anyway, since it's the same server a
production deployment would validate submissions against:

```
$ bash scripts/verify-terminology.sh
▸ $expand bd-condition-icd11-diagnosis-valueset-fixed — expect 1D40, 1F4Z
  ✓ expanded to ['1D40', '1F4Z']
▸ $validate-code — 10828004 (Positive, in-set) — expect true
  ✓ accepted
▸ $validate-code — 260415000 (Not detected, out-of-set) — expect false
  ✓ rejected
▸ $translate — legacy 'pos' -> SNOMED
  ✓ translated to SNOMED 10828004 (Positive)
▸ $translate — legacy 'neg' -> SNOMED
  ✓ translated to SNOMED 260385009 (Negative)
```

Unlike the static validator, HAPI's terminology module only needs the
**ValueSet resource itself** to answer `$expand`/`$validate-code` for an
enumerated compose — it doesn't need SNOMED CT or LOINC loaded, because it's
just checking list membership, not resolving an external hierarchy. That's the
real case for running a terminology server rather than relying on `validator_cli`
alone.

## Why FSH, unlike the Questionnaire

Phase J deliberately built the Questionnaire in C#, not FSH, because nothing
else needed to validate *against* it. CodeSystem/ValueSet/ConceptMap are the
opposite case: `validator_cli` needs them as files (via `-ig`) to check
`ProhoriObservation`'s bindings, and `scripts/load-terminology.sh` needs the
identical JSON to PUT onto a live server. FSH's `sushi . --snapshot` produces
one set of files both consumers use directly — no export step, no second
source of truth, and FSH's `CodeSystem:`/`ValueSet:` grammar (and instance
syntax for `ConceptMap`, whose FSH keyword SUSHI 3.20 doesn't yet have — see
`ProhoriTerminology.fsh`) is genuinely more idiomatic for this than it would
have been for a Questionnaire.

## Fixing BD-Core's empty ICD-11 ValueSet

Phase F found that `bd.fhir.core#0.4.6`'s `bd-condition-icd11-diagnosis-valueset`
ships with an **empty `compose`**, making its own required binding
unsubmittable (worked around by recording the diagnosis on
`Encounter.reasonCode` instead — see
[`docs/bd-core-submission.md`](bd-core-submission.md)). `1D40` (Dengue fever)
and `1F4Z` (Malaria, unspecified) are both valid WHO ICD-11 MMS codes, confirmed
by the DGHS sandbox's own error message when it rejected the wrong `bd-condition`.

`bd-condition-icd11-diagnosis-valueset-fixed` is the ValueSet BD-Core should
have shipped: those two codes, enumerated, `$expand`-able and
`$validate-code`-able against a live server with no external ICD-11 lookup
needed. It lives in this repo, not upstream — filing an issue or PR against
`git.dghs.gov.bd`'s BD-Core-FHIR-IG is a real action against a government
repository under someone else's ownership, so it isn't done automatically here.
A draft issue, ready to file:

> **Title:** `bd-condition-icd11-diagnosis-valueset` ships with an empty compose
>
> **Body:** `ValueSet/bd-condition-icd11-diagnosis-valueset` in
> `bd.fhir.core#0.4.6` has no `compose.include`, so no code can satisfy
> `bd-condition.code`'s required binding — any `bd-condition` instance is
> rejected by both the official validator and the live sandbox. Confirmed
> working codes for the two conditions this ValueSet's title names (dengue,
> malaria): ICD-11 MMS `1D40` (Dengue fever) and `1F4Z` (Malaria, unspecified).
> A minimal fix and a live `$expand`/`$validate-code` demonstration:
> `<link to bd-condition-icd11-diagnosis-valueset-fixed on GitHub>`.

## `$translate` and the legacy-import path

A third way into the same case Bundle, alongside the typed API (Phase C) and
the SDC form (Phase J): `POST /legacy-import/cases` accepts a
`LegacyCaseSubmission` — identical to `CaseSubmission` except
`rdtResultLegacy` is still `"pos"`/`"neg"`, the way a legacy ODK export would
hand it over. `TerminologyClient.TranslateRdtResultAsync` calls `$translate`
against the configured FHIR server's `prohori-rdt-result-legacy-to-snomed`
ConceptMap; the resolved SNOMED coding becomes a normal `CaseSubmission`, which
goes through the *same* `CaseBundleBuilder` / `FhirCaseService` every other
path uses. `?dryRun=true` returns the built Bundle without submitting.

If the ConceptMap isn't loaded on the target server, the endpoint returns a
`502` naming the problem (`scripts/load-terminology.sh`) rather than a bare
gateway error.

## Run it

```bash
# macOS: colima start --cpu 4 --memory 6 (once)
docker compose -f deploy/docker-compose.yml up -d hapi
cd ig && sushi . --snapshot && cd ..
bash scripts/validate-ig.sh          # offline: ProhoriPatient + ProhoriObservation structure
bash scripts/load-terminology.sh     # PUT CodeSystem/ValueSets/ConceptMap onto local HAPI
bash scripts/verify-terminology.sh   # live: $expand, $validate-code, $translate
```
