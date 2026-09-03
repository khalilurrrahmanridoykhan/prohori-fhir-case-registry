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
