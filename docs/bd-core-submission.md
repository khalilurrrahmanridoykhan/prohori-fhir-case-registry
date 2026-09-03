# Phase F — BD-Core conformance + live submission

**Goal met** (2026-09-03):

- the official HL7 validator reports **0 errors** against `bd.fhir.core#0.4.6`
- the **live DGHS sandbox** (`https://sandbox.fhir.dghs.gov.bd/fhir`, HAPI 7.4.0)
  **accepted** the Bundle

Reproduce: `bash scripts/bd-core.sh --submit`

---

## What Prohori submits

A `transaction` Bundle of **5** BD-Core-profiled resources, built by
`BdCoreBundleBuilder` (`POST /bd-core/cases`, or `?dryRun=true` to inspect):

| Resource | Profile | Key BD-Core requirements met |
| :--- | :--- | :--- |
| `Organization` | `bd-organization` | facility identifier (HRM code, type `FI`) |
| `Practitioner` | `bd-practitioner` | HRIS identifier |
| `Patient` | `bd-patient` | **UHID** + **NID** identifiers (NID with `bd-identifier-type` coding); `name.use` + `name.text` with **`bd-name-en`** / **`bd-name-bn`** extensions; **`bd-address`** with **division** + **upazila** geocode extensions, district code, `country = BD` |
| `Encounter` | `bd-encounter` | identifier, `class = AMB`, `subject`, `participant` (+ period, Practitioner), `serviceProvider` (Organization); ICD-11 MMS diagnosis on `reasonCode` |
| `Observation` | `bd-observation` | identifier, `category`, LOINC `code`, `subject` (+ display), `encounter`, `performer` |

`If-None-Exist` on the facility code / HRIS code / NID means re-submitting the
same case reuses the Organization, Practitioner and Patient (no duplicates).

## The live submission

```
$ bash scripts/bd-core.sh --submit
▸ validating against bd.fhir.core…
Success: 0 errors, 9 warnings, 8 notes
▸ submitting to https://sandbox.fhir.dghs.gov.bd/fhir …
  HTTP 200
  200 OK       Organization/7837ab49-e92e-43ce-9ccc-2ba475cd1222/_history/1
  200 OK       Practitioner/0825a91d-c265-4233-9f52-37d91896cff9/_history/1
  200 OK       Patient/8ba33898-20fe-4ce0-8bca-996cdb02bf52/_history/1
  201 Created  Encounter/ce03b62d-cab1-4a1f-93e8-b7c87f328e14/_history/1
  201 Created  Observation/db40fcfa-6799-437d-9ed2-9dd6775d3cff/_history/1
```

Verify on the sandbox:
`https://sandbox.fhir.dghs.gov.bd/fhir/Patient/8ba33898-20fe-4ce0-8bca-996cdb02bf52/$everything`

The remaining 9 warnings are all `dom-6` ("resource should have a narrative",
best-practice only) and a `v2-0203` binding note on the facility identifier type
— none block acceptance.

---

## Finding: `bd-condition` is unsubmittable in BD-Core v0.4.6

`bd-condition` binds `Condition.code` with **strength `required`** to
`ValueSet/bd-condition-icd11-diagnosis-valueset`. In the `bd.fhir.core#0.4.6`
package that ValueSet has **an empty `compose`** (its narrative lists no rules),
so *no* code can satisfy the binding — the validator and the DGHS sandbox both
reject any `bd-condition` instance:

```
error: None of the codings provided are in the value set 'Bangladesh ICD-11 MMS
Condition ValueSet (Diagnosis and Finding)'
(…/bd-condition-icd11-diagnosis-valueset|0.4.6), and a coding from this value set
is required (codes = http://id.who.int/icd/release/11/mms#1D40,
http://snomed.info/sct#38362002)
```

`1D40` (ICD-11 MMS "Dengue fever") is a valid WHO code; the ValueSet simply
hasn't been populated yet. Until BD-Core fills it in, Prohori records the
diagnosis as **`Encounter.reasonCode`** (ICD-11 MMS), which is unconstrained by
`bd-encounter` and accepted.

---

## Not done (optional add-ons from the plan)

GIS `Location` mapping, ODK `Questionnaire` conversion, and the DHIS2
`MeasureReport` bridge are deferred — the primary goal (BD-Core conformance +
live submission) is met.
