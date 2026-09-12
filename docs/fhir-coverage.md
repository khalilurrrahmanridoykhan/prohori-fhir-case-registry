# FHIR coverage matrix

| Domain | Phase / evidence |
| --- | --- |
| REST, search, bundles, history, OperationOutcome | A–C complete |
| Profiling, FSH/SUSHI, conformance, national IG | E–F complete |
| SMART App Launch, OAuth2, OIDC, scopes | H implemented and live verified — [launch guide](smart-launch.md), [results and Inferno caveat](phase-h-verification.md) |
| JSON Patch, FHIRPath Patch, ETag / If-Match, return preferences | H complete — version-required API writes and live stale replay |
| respond-async negotiation | I implemented — real HAPI 202/poll/manifest/NDJSON sequence |
| SMART Backend Services and Bulk Data export | I implemented — [RS384, cohort export and incremental aggregates](bulk-export.md) |
| Questionnaire, SDC populate / extract | J implemented — [Questionnaire → $populate/$extract → the same case Bundle](sdc.md) |
| Terminology services | K implemented — [$expand/$validate-code/$translate live against HAPI; fixes BD-Core's empty ICD-11 ValueSet](terminology.md) |
| HL7 v2, StructureMap | L planned |
| CQL, Measure, MeasureReport | M planned |
| IG Publisher and CapabilityStatement | N planned |
| Subscriptions, audit, consent | O planned |
| Certification and portfolio consolidation | P planned |
