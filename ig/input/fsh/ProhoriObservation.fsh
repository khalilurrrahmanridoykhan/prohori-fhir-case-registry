Profile: ProhoriObservation
Parent: Observation
Id: prohori-observation
Title: "Prohori RDT Observation"
Description: """
The rapid-test-result Observation a Prohori case Bundle carries. Unlike
ProhoriPatient (Phase E), its coded elements are **required-bound** to
Prohori's own ValueSets — the terminology work of Phase K:

* `code` — the LOINC test performed
* `valueCodeableConcept` — the SNOMED CT result
"""
* ^status = #draft
* ^experimental = true

* status 1..1 MS
* code 1..1 MS
* code from ProhoriRdtTestValueSet (required)
* subject 1..1 MS
* valueCodeableConcept 1..1 MS
* valueCodeableConcept from ProhoriRdtResultValueSet (required)
