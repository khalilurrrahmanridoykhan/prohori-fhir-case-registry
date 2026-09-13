Instance: prohori-patient-example
InstanceOf: ProhoriPatient
Usage: #example
Title: "Conformant Prohori Patient"
Description: "A patient that satisfies the ProhoriPatient profile."
* identifier[nationalId].system = "http://health.gov.bd/sid"
* identifier[nationalId].value = "19942691012345678"
* active = true
* name.use = #official
* name.family = "Khan"
* name.given = "Rahman"
* gender = #male
* birthDate = "1995-06-15"
* address.use = #home
* address.city = "Dhaka"
* address.district = "Dhaka"
* address.country = "Bangladesh"

Instance: prohori-encounter-example
InstanceOf: ProhoriEncounter
Usage: #example
Title: "Conformant Prohori Encounter"
Description: "The field visit for prohori-patient-example — a real CaseBundleBuilder shape."
* status = #finished
* class = http://terminology.hl7.org/CodeSystem/v3-ActCode#FLD "field"
* subject = Reference(prohori-patient-example)
* period.start = "2026-08-14T09:20:00+06:00"
* period.end = "2026-08-14T09:50:00+06:00"

Instance: prohori-observation-example
InstanceOf: ProhoriObservation
Usage: #example
Title: "Conformant Prohori Observation"
Description: "The dengue RDT result for the same visit — positive."
* status = #final
* category = http://terminology.hl7.org/CodeSystem/observation-category#laboratory
* code = http://loinc.org#42239-4 "Dengue virus NS1 Ag [Presence] in Serum or Plasma by Immunoassay"
* subject = Reference(prohori-patient-example)
* encounter = Reference(prohori-encounter-example)
* effectiveDateTime = "2026-08-14T09:20:00+06:00"
* valueCodeableConcept = http://snomed.info/sct#10828004 "Positive"

Instance: prohori-condition-example
InstanceOf: ProhoriCondition
Usage: #example
Title: "Conformant Prohori Condition"
Description: "The confirmed diagnosis once the RDT above came back positive."
* clinicalStatus = http://terminology.hl7.org/CodeSystem/condition-clinical#active
* verificationStatus = http://terminology.hl7.org/CodeSystem/condition-ver-status#confirmed
* code = http://snomed.info/sct#38362002 "Dengue fever"
* subject = Reference(prohori-patient-example)
* encounter = Reference(prohori-encounter-example)
* onsetDateTime = "2026-08-14T09:20:00+06:00"
* recordedDate = "2026-08-14"
