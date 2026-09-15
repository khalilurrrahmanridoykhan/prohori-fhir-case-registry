// Phase N — what Prohori.Api actually implements. Not a general-purpose FHIR server: it's a
// specialized write facade in front of one (POST /cases, $extract, $populate, /legacy-import,
// $evaluate-measure). Reads of case data go straight from the dashboard to the FHIR server —
// see docs/sdc.md, docs/terminology.md and docs/measures.md for each operation's own write-up.

Instance: prohori-api-capabilitystatement
InstanceOf: CapabilityStatement
Usage: #definition
Title: "Prohori.Api CapabilityStatement"
Description: "What Prohori.Api implements, checked against Program.cs's actual endpoint mappings — nothing declared here that the code doesn't do."
* url = "https://prohori.health/fhir/CapabilityStatement/prohori-api-capabilitystatement"
* version = "0.2.0"
* name = "ProhoriApiCapabilityStatement"
* status = #draft
* experimental = true
* date = "2026-09-15"
* publisher = "Khalilur Rahman Ridoy Khan"
* kind = #instance
* implementation.description = "The running Prohori.Api instance — see docs/publishing-the-ig.md for how this IG is built and where the API is (or isn't yet) deployed."
* implementation.url = "https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry"
* fhirVersion = #4.0.1
* format[0] = #json
* format[1] = #application/fhir+json

* rest[0].mode = #server
* rest[0].documentation = "A write facade, not a general-purpose FHIR server: builds and submits transaction Bundles, and evaluates one quality measure. Every write ends at CaseBundleBuilder.Build() regardless of entry point (Phase C typed API, Phase J SDC form, Phase K legacy-import, Phase L's separate V2Gateway)."

* rest[0].security.service = http://terminology.hl7.org/CodeSystem/restful-security-service#OAuth "OAuth"
* rest[0].security.description = "Bearer token, CaseWrite scope, required on every write except the public Questionnaire/Measure reads. See docs/smart-launch.md."

* rest[0].resource[0].type = #Patient
* rest[0].resource[0].documentation = "Conditional create via POST /cases (and the other write paths); PATCH/PUT /cases/{id} with mandatory If-Match (Phase H). GET /patients/{nationalId} returns $everything, gated by that patient's Consent (see the Consent resource entry below) — 403 + OperationOutcome once denied, or ?breakGlass=true with the break-glass scope."
* rest[0].resource[0].interaction[0].code = #create
* rest[0].resource[0].interaction[1].code = #update
* rest[0].resource[0].interaction[2].code = #patch
* rest[0].resource[0].interaction[3].code = #read

* rest[0].resource[1].type = #QuestionnaireResponse
* rest[0].resource[1].documentation = "POST /questionnaire-response/$extract builds and submits the case Bundle from a filled-in response. See docs/sdc.md."
* rest[0].resource[1].interaction[0].code = #create
* rest[0].resource[1].operation[0].name = "extract"
* rest[0].resource[1].operation[0].definition = "http://hl7.org/fhir/uv/sdc/OperationDefinition/QuestionnaireResponse-extract"

* rest[0].resource[2].type = #Questionnaire
* rest[0].resource[2].documentation = "GET /questionnaire-response/questionnaire (public) serves the field-intake form; POST /questionnaire-response/$populate prefills it for a returning patient."
* rest[0].resource[2].interaction[0].code = #read
* rest[0].resource[2].operation[0].name = "populate"
* rest[0].resource[2].operation[0].definition = "http://hl7.org/fhir/uv/sdc/OperationDefinition/Questionnaire-populate"

* rest[0].resource[3].type = #Measure
* rest[0].resource[3].documentation = "GET /measure/$evaluate-measure (public) — computed by MeasureEvaluator.cs, not a live CQL engine. See docs/measures.md for why."
* rest[0].resource[3].operation[0].name = "evaluate-measure"
* rest[0].resource[3].operation[0].definition = "http://hl7.org/fhir/OperationDefinition/Measure-evaluate-measure"

* rest[0].resource[4].type = #ConceptMap
* rest[0].resource[4].documentation = "$translate backs POST /legacy-import/cases, resolving a legacy ODK RDT code to SNOMED CT before the normal case Bundle is built. See docs/terminology.md."
* rest[0].resource[4].operation[0].name = "translate"
* rest[0].resource[4].operation[0].definition = "http://hl7.org/fhir/OperationDefinition/ConceptMap-translate"

* rest[0].resource[5].type = #Consent
* rest[0].resource[5].documentation = "A default-permit Consent is created in the same transaction as every new patient's first case (conditional create, keyed on the patient's own National ID, not chained through the reference — see docs/realtime-provenance-consent.md for why). PUT /patients/{nationalId}/consent toggles it; GET /patients/{nationalId} (Patient resource, below) is refused with 403 once it's set to deny."
* rest[0].resource[5].interaction[0].code = #create
* rest[0].resource[5].interaction[1].code = #update

* rest[0].resource[6].type = #AuditEvent
* rest[0].resource[6].documentation = "One AuditEvent is appended to every write transaction, naming every resource it wrote and the authenticated caller. A break-glass read (Patient, below) also creates one, using the v3 BTG purpose-of-use — recorded before the read happens."
* rest[0].resource[6].interaction[0].code = #create
