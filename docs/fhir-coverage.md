# FHIR coverage matrix

| Domain | Phase / evidence |
| --- | --- |
| REST, search, bundles, history, OperationOutcome | A–C complete |
| Profiling, FSH/SUSHI, conformance, national IG | E–F complete |
| SMART App Launch, OAuth2, OIDC, scopes | H implemented and live verified — [launch guide](smart-launch.md), [results and Inferno caveat](phase-h-verification.md) |
| JSON Patch, FHIRPath Patch, ETag / If-Match, return preferences | H complete — version-required API writes and live stale replay |
| respond-async negotiation | H documented synchronous fallback; actual async jobs deferred to I |
| SMART Backend Services and Bulk Data export | I planned |
| Questionnaire, SDC populate / extract | J planned |
| Terminology services | K planned |
| HL7 v2, StructureMap | L planned |
| CQL, Measure, MeasureReport | M planned |
| IG Publisher and CapabilityStatement | N planned |
| Subscriptions, audit, consent | O planned |
| Certification and portfolio consolidation | P planned |
