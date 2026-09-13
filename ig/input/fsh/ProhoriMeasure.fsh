// Phase M — CQL, Measure & MeasureReport. See docs/measures.md.
//
// The Library's actual CQL text lives at ig/input/cql/ProhoriDiseasePositivity.cql — a real,
// reviewable file, not embedded here as base64 (there is no live CQL engine to consume it in
// this phase; keeping it a plain .cql file, same as Phase L's .map, is easier to read and to
// keep honest than a machine-encoded blob nothing executes).

Instance: prohori-disease-positivity
InstanceOf: Library
Usage: #definition
Title: "Prohori disease positivity"
Description: "Field visits in a period, and how many were RDT-positive — declared here, evaluated by MeasureEvaluator.cs. See ig/input/cql/ProhoriDiseasePositivity.cql for the CQL."
* url = "https://prohori.health/fhir/Library/prohori-disease-positivity"
* version = "1.0.0"
* name = "ProhoriDiseasePositivity"
* status = #draft
* type = http://terminology.hl7.org/CodeSystem/library-type#logic-library "Logic Library"
* subjectCodeableConcept = http://hl7.org/fhir/resource-types#Patient "Patient"
* content[0].contentType = #text/cql
* content[0].data = "bGlicmFyeSBQcm9ob3JpRGlzZWFzZVBvc2l0aXZpdHkgdmVyc2lvbiAnMS4wLjAnCgovKgogKiBUaGUgcG9wdWxhdGlvbnMgTWVhc3VyZUV2YWx1YXRvci5jcyBjb21wdXRlcyBieSBoYW5kIGluIEMjIOKAlCBzZWUgZG9jcy9tZWFzdXJlcy5tZCBmb3Igd2h5CiAqIHRoaXMgaXMgZGVjbGFyZWQgYnV0IG5vdCBpbnRlcnByZXRlZCBieSBhIGxpdmUgQ1FMIGVuZ2luZSAoY2hlY2tlZCBmaXJzdDogbG9jYWwgSEFQSSdzIG93bgogKiBjbGluaWNhbC1yZWFzb25pbmcgbW9kdWxlIGxvYWRzLCBidXQgJGV2YWx1YXRlLW1lYXN1cmUgaXNuJ3Qgd2lyZWQgdXAgZXZlbiB3aXRoIGl0IGVuYWJsZWQpLgogKgogKiBPbmUgZmllbGQgdmlzaXQgKEVuY291bnRlcikgaW4gdGhlIG1lYXN1cmVtZW50IHBlcmlvZCBpcyB0aGUgdW5pdCBvZiBhbmFseXNpczsgYSB2aXNpdCBpcwogKiBudW1lcmF0b3ItcG9zaXRpdmUgaWYgaXRzIGxpbmtlZCBPYnNlcnZhdGlvbidzIFJEVCByZXN1bHQgaXMgU05PTUVEIENUIDEwODI4MDA0IChQb3NpdGl2ZSkuCiAqIE5vIGV4Y2x1c2lvbnMgaW4gdGhpcyBtZWFzdXJlLCBzbyBJbml0aWFsIFBvcHVsYXRpb24gPT0gRGVub21pbmF0b3IuCiAqLwoKdXNpbmcgRkhJUiB2ZXJzaW9uICc0LjAuMScKCmluY2x1ZGUgRkhJUkhlbHBlcnMgdmVyc2lvbiAnNC4wLjEnIGNhbGxlZCBGSElSSGVscGVycwoKY29kZXN5c3RlbSAiU05PTUVEIjogJ2h0dHA6Ly9zbm9tZWQuaW5mby9zY3QnCgpwYXJhbWV0ZXIgIk1lYXN1cmVtZW50IFBlcmlvZCIgSW50ZXJ2YWw8RGF0ZVRpbWU+Cgpjb250ZXh0IFBhdGllbnQKCmRlZmluZSAiRmllbGQgVmlzaXRzIEluIFBlcmlvZCI6CiAgW0VuY291bnRlcl0gRQogICAgd2hlcmUgRS5wZXJpb2Quc3RhcnQgZHVyaW5nICJNZWFzdXJlbWVudCBQZXJpb2QiCgpkZWZpbmUgIlBvc2l0aXZlIFJEVCBPYnNlcnZhdGlvbnMgSW4gUGVyaW9kIjoKICBbT2JzZXJ2YXRpb25dIE8KICAgIHdoZXJlIE8uZWZmZWN0aXZlIGR1cmluZyAiTWVhc3VyZW1lbnQgUGVyaW9kIgogICAgICBhbmQgTy52YWx1ZS5jb2RpbmcuY29kZSBjb250YWlucyAnMTA4MjgwMDQnIC8vIFNOT01FRCBDVDogUG9zaXRpdmUKCmRlZmluZSAiSW5pdGlhbCBQb3B1bGF0aW9uIjoKICBleGlzdHMoIkZpZWxkIFZpc2l0cyBJbiBQZXJpb2QiKQoKZGVmaW5lICJEZW5vbWluYXRvciI6CiAgIkluaXRpYWwgUG9wdWxhdGlvbiIKCmRlZmluZSAiTnVtZXJhdG9yIjoKICBleGlzdHMoIkZpZWxkIFZpc2l0cyBJbiBQZXJpb2QiIEUKICAgIHdoZXJlIGV4aXN0cygiUG9zaXRpdmUgUkRUIE9ic2VydmF0aW9ucyBJbiBQZXJpb2QiIE8gd2hlcmUgTy5lbmNvdW50ZXIucmVmZXJlbmNlID0gJ0VuY291bnRlci8nICsgRS5pZCkpCgpkZWZpbmUgIkNpdHkiOgogIFBhdGllbnQuYWRkcmVzcy5jaXR5Cg=="

Instance: prohori-disease-positivity-measure
InstanceOf: Measure
Usage: #definition
Title: "Prohori disease positivity measure"
Description: "Proportion of field visits, in a period, with a positive RDT result — stratified by city."
* url = "https://prohori.health/fhir/Measure/prohori-disease-positivity-measure"
* version = "1.0.0"
* name = "ProhoriDiseasePositivityMeasure"
* status = #draft
* subjectCodeableConcept = http://hl7.org/fhir/resource-types#Encounter "Encounter"
* library = "https://prohori.health/fhir/Library/prohori-disease-positivity"
* scoring = http://terminology.hl7.org/CodeSystem/measure-scoring#proportion "Proportion"
* type = http://terminology.hl7.org/CodeSystem/measure-type#outcome "Outcome"
* improvementNotation = http://terminology.hl7.org/CodeSystem/measure-improvement-notation#increase "Increased score indicates improvement"
* group[0].population[0].code = http://terminology.hl7.org/CodeSystem/measure-population#initial-population "Initial Population"
* group[0].population[0].criteria.language = #text/cql
* group[0].population[0].criteria.expression = "Initial Population"
* group[0].population[1].code = http://terminology.hl7.org/CodeSystem/measure-population#denominator "Denominator"
* group[0].population[1].criteria.language = #text/cql
* group[0].population[1].criteria.expression = "Denominator"
* group[0].population[2].code = http://terminology.hl7.org/CodeSystem/measure-population#numerator "Numerator"
* group[0].population[2].criteria.language = #text/cql
* group[0].population[2].criteria.expression = "Numerator"
* group[0].stratifier[0].code.text = "City"
* group[0].stratifier[0].criteria.language = #text/cql
* group[0].stratifier[0].criteria.expression = "City"
