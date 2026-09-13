### Prohori — FHIR-native field case registry

*Prohori* (প্রহরী, "sentinel") is a dengue/malaria field case registry built
from scratch against HL7 FHIR R4, one phase at a time, all against real FHIR
servers — the public HAPI sandbox, a self-hosted HAPI instance, and Bangladesh's
own national FHIR sandbox. This Implementation Guide is the FHIR Solutions
Architect artifact that phase produces: the profiles the registry enforces,
the terminology it binds to, a real HL7 v2 → FHIR mapping, and a computable
quality measure — all in one place, alongside the running system itself.

**Live dashboard:** [prohori-fhir-case-registry.vercel.app](https://prohori-fhir-case-registry.vercel.app)
(reads Bangladesh's national FHIR sandbox directly)
**Source, tests, CI, every decision recorded:**
[github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry)

All data throughout this project — profile examples, the demo cohort, every
screenshot — is synthetic. Not a medical device; not for use with real
patient data.

#### What's in this IG

| | |
| :--- | :--- |
| **Profiles** | `ProhoriPatient`, `ProhoriEncounter`, `ProhoriObservation`, `ProhoriCondition` — see [Artifacts Summary](artifacts.html) |
| **Terminology** | A CodeSystem, three ValueSets (including a fix for a real defect found in Bangladesh's national BD-Core-FHIR-IG), and a ConceptMap |
| **Questionnaire** | Field intake as SDC — `$populate` / `$extract` into the same case Bundle the typed API builds |
| **CQL / Measure** | Field-visit positivity as a computable quality measure, evaluated into a real `MeasureReport` |
| **CapabilityStatement** | What `Prohori.Api` actually supports — see [Architecture](architecture.html) |
| **HL7 v2 mapping** | A real FHIR Mapping Language declaration for ADT → Patient/Encounter |

See [Architecture](architecture.html) for how the pieces fit together, and
[Phases A–P](phases.html) for the build log — every phase's goal, what it
proved, and links to the merged pull request.

#### Why this IG looks the way it does

Two profiles here (`ProhoriPatient`, `ProhoriObservation`) carry `required`
terminology bindings that the offline FHIR validator genuinely cannot enforce
without a live terminology server — documented in
[Terminology](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/blob/main/docs/terminology.md),
proved live instead. The HL7 v2 mapping and the CQL Measure are both declared
in their real, standards-native languages (FHIR Mapping Language, CQL) but
evaluated by hand-written C# rather than a live `$transform` / `$evaluate-measure`
engine — checked first, each time, rather than assumed; see
[`docs/hl7v2-to-fhir.md`](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/blob/main/docs/hl7v2-to-fhir.md)
and
[`docs/measures.md`](https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry/blob/main/docs/measures.md).
An IG that only showed the parts that worked cleanly on the first try
wouldn't be an honest one.
