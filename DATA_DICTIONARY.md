# Data dictionary

All repository fixtures and demonstration records are synthetic. The API input
contract is `src/Prohori.Api/Models/CaseSubmission.cs`; BD-Core adds the
geographic and provider codes in `BdCoreCaseSubmission.cs`.

| Field | Type / unit | Allowed values and meaning |
| --- | --- | --- |
| patient.nationalId | String, 10–17 digits | Synthetic NID-shaped identifier; never a real person's NID |
| patient.familyName | String, 1–100 characters | Synthetic family name |
| patient.givenNames | Array of strings | Synthetic given names; empty array permitted |
| patient.gender | FHIR administrative gender code | male, female, other, unknown |
| patient.birthDate | ISO date | YYYY-MM-DD |
| patient.city / district | String, 1–100 characters | Synthetic residence labels |
| disease | Enum | dengue or malaria |
| rdtResult | Enum | positive or negative |
| visitDate | Timestamp with UTC offset | Time of the synthetic field visit |
| created | Array of FHIR references | Returned resource/version locations |
| meta.versionId / ETag | Opaque version identifier | Use for If-Match; do not interpret as a clinical value |
| patientId | FHIR id | Launch patient context, not an NID |

FHIR resources carry their native field definitions: Patient demographics,
Encounter visit, Observation result and optional Condition diagnosis. Missing
optional values are omitted; there is no numeric missing-value sentinel.
The dashboard renders missing labels as `—`, unmatched disease codes as
`Unknown`, and absent RDT interpretations as `No result`. These are display
states, not negative test results. Generic sandbox encounters may be unrelated
to dengue or malaria; their presence does not imply either diagnosis.

BD-Core geography and terminology mapping are documented in
[the submission notes](docs/bd-core-submission.md) and the source builders.
