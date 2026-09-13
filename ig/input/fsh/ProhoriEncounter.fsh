Profile: ProhoriEncounter
Parent: Encounter
Id: prohori-encounter
Title: "Prohori Encounter"
Description: """
A field visit in the Prohori case registry — a community health worker
recording a patient contact. Built by `CaseBundleBuilder`/`V2ToFhirMapper`;
this profile is what those builders actually populate, made explicit.
"""
* ^status = #draft
* ^experimental = true

* status 1..1 MS
* class 1..1 MS
* subject 1..1 MS
* period 1..1 MS
* period.start 1..1 MS
