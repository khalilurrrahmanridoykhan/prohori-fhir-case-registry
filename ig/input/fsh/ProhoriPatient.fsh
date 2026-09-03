Profile: ProhoriPatient
Parent: Patient
Id: prohori-patient
Title: "Prohori Patient"
Description: """
A patient in the Prohori field case registry. Tightens the base Patient:

* a **Bangladesh National ID** identifier is required
* `name`, `gender` and `birthDate` are required (must-support)
"""
* ^status = #draft
* ^experimental = true

* identifier 1..* MS
* identifier ^slicing.discriminator.type = #value
* identifier ^slicing.discriminator.path = "system"
* identifier ^slicing.rules = #open
* identifier ^slicing.description = "Slice identifiers by their system."
* identifier contains nationalId 1..1 MS
* identifier[nationalId].system 1..1
* identifier[nationalId].system = "http://health.gov.bd/sid" (exactly)
* identifier[nationalId].value 1..1
* identifier[nationalId].value obeys prohori-nid-digits

* name 1..* MS
* name.family 1..1

* gender 1..1 MS
* birthDate 1..1 MS

Invariant: prohori-nid-digits
Description: "National ID is 10 to 17 digits."
Severity: #error
Expression: "matches('^[0-9]{10,17}$')"
