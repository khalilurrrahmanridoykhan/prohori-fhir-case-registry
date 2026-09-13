// Phase K — terminology artifacts. See docs/terminology.md.
// ProhoriDiagnosisValueSet added in Phase N, binding ProhoriCondition.code.

ValueSet: ProhoriDiagnosisValueSet
Id: prohori-diagnosis-valueset
Title: "Prohori Diagnosis (SNOMED CT)"
Description: "The two diseases Prohori tracks, coded — binds ProhoriCondition.code."
* ^status = #draft
* http://snomed.info/sct#38362002 "Dengue fever"
* http://snomed.info/sct#61462000 "Malaria"

CodeSystem: ProhoriRdtResultLegacy
Id: prohori-rdt-result-legacy
Title: "Prohori RDT Result (legacy ODK codes)"
Description: """
The plain-language codes a legacy ODK/KoBo `select_one` export uses for a
rapid-test result, before anything is coded to SNOMED CT. Fed through
`ConceptMap/prohori-rdt-result-legacy-to-snomed` via `$translate`.
"""
* ^status = #draft
* ^experimental = true
* ^caseSensitive = true
* ^content = #complete
* #pos "Positive (legacy ODK code)"
* #neg "Negative (legacy ODK code)"

ValueSet: ProhoriRdtResultLegacyValueSet
Id: prohori-rdt-result-legacy-valueset
Title: "Prohori RDT Result (legacy ODK codes) — ValueSet"
Description: "Both codes from ProhoriRdtResultLegacy — ConceptMap.sourceCanonical must be a ValueSet, not a CodeSystem directly (R4 modeling rule), so this exists purely to satisfy that."
* ^status = #draft
* include codes from system ProhoriRdtResultLegacy

ValueSet: ProhoriRdtResultValueSet
Id: prohori-rdt-result-valueset
Title: "Prohori RDT Result (SNOMED CT)"
Description: "The two SNOMED CT codes Prohori's Observation.valueCodeableConcept binds to."
* ^status = #draft
* http://snomed.info/sct#10828004 "Positive"
* http://snomed.info/sct#260385009 "Negative"

ValueSet: ProhoriRdtTestValueSet
Id: prohori-rdt-test-valueset
Title: "Prohori RDT Test (LOINC)"
Description: "The two LOINC test codes Prohori's Observation.code binds to."
* ^status = #draft
* http://loinc.org#42239-4 "Dengue virus NS1 Ag [Presence] in Serum or Plasma by Immunoassay"
* http://loinc.org#70569-9 "Plasmodium sp Ag [Identifier] in Blood by Rapid immunoassay"

ValueSet: BdConditionIcd11DiagnosisValueSetFixed
Id: bd-condition-icd11-diagnosis-valueset-fixed
Title: "BD-Core ICD-11 diagnosis ValueSet — fixed"
Description: """
BD-Core-FHIR-IG 0.4.6 ships `bd-condition-icd11-diagnosis-valueset` with an
**empty** `compose`, which makes its own required binding — on `bd-condition`
— unsubmittable (see docs/bd-core-submission.md; worked around there by
recording the diagnosis on `Encounter.reasonCode` instead, which carries no
required binding). This is the ValueSet it should have shipped: an enumerated
`compose` over ICD-11 MMS, scoped to the two diseases Prohori tracks. `$expand`
and `$validate-code` both resolve against it with no external terminology
server needed — see docs/terminology.md.
"""
* ^status = #draft
* http://id.who.int/icd/release/11/mms#1D40 "Dengue fever"
* http://id.who.int/icd/release/11/mms#1F4Z "Malaria, unspecified"

// SUSHI 3.20's FSH grammar has no dedicated `ConceptMap:` keyword — author it as
// a definitional Instance, same mechanism examples.fsh uses for example instances.
Instance: prohori-rdt-result-legacy-to-snomed
InstanceOf: ConceptMap
Usage: #definition
Title: "Legacy RDT result codes → SNOMED CT"
Description: "Translates a legacy ODK export's plain-language RDT result codes to SNOMED CT, via $translate."
* url = "https://prohori.health/fhir/ConceptMap/prohori-rdt-result-legacy-to-snomed"
* status = #draft
// sourceCanonical/targetCanonical must reference ValueSets, not CodeSystems directly (an R4
// modeling rule the IG Publisher enforces) — group.source/group.target below are the actual
// CodeSystem URIs $translate matches codes against.
* sourceCanonical = "https://prohori.health/fhir/ValueSet/prohori-rdt-result-legacy-valueset"
* targetCanonical = "https://prohori.health/fhir/ValueSet/prohori-rdt-result-valueset"
* group[0].source = "https://prohori.health/fhir/CodeSystem/prohori-rdt-result-legacy"
* group[0].target = "http://snomed.info/sct"
* group[0].element[0].code = #pos
* group[0].element[0].target[0].code = #10828004
* group[0].element[0].target[0].display = "Positive"
* group[0].element[0].target[0].equivalence = #equivalent
* group[0].element[1].code = #neg
* group[0].element[1].target[0].code = #260385009
* group[0].element[1].target[0].display = "Negative"
* group[0].element[1].target[0].equivalence = #equivalent
