Profile: ProhoriCondition
Parent: Condition
Id: prohori-condition
Title: "Prohori Condition"
Description: """
A confirmed field diagnosis — dengue or malaria — recorded when a case's RDT
result is positive. `code` is required-bound to `ProhoriDiagnosisValueSet`
(Phase K/N terminology), the same SNOMED codes `CaseBundleBuilder` writes.
"""
* ^status = #draft
* ^experimental = true

* clinicalStatus 1..1 MS
* verificationStatus 1..1 MS
* code 1..1 MS
* code from ProhoriDiagnosisValueSet (required)
* subject 1..1 MS
* encounter 1..1 MS
* onset[x] 1..1 MS
* recordedDate 1..1 MS
