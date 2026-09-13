# Structured Data Capture (Phase J)

Field intake as a FHIR `Questionnaire`. Submitting the New Case form calls `$extract`,
which builds the *same* case Bundle the typed `POST /cases` endpoint builds — extraction
maps answers into a `CaseSubmission`, then hands it to the same `CaseBundleBuilder` Phase C
wrote. There is exactly one place that turns a case into FHIR resources.

## Why the Questionnaire is built in C#, not FSH

Every other Prohori profile (`ProhoriPatient`, Phase E) is FHIR Shorthand: a **constraint**
on instances a server receives, authored once and enforced on data it never wrote itself.
A `Questionnaire` has no such split — the resource returned by
`GET /questionnaire-response/questionnaire` **is** the artifact; there's no separate
"real-world instance" for it to constrain. Defining it a second time in FSH would just be
a second place a linkId could drift from the extraction code that reads it back. So
[`QuestionnaireCatalog.cs`](../src/Prohori.Api/Fhir/QuestionnaireCatalog.cs) is the single
source of truth, exported for CI validation with `--export-questionnaire` (mirroring
`--export-bd-core`, Phase F) and checked against the base FHIR R4 spec by
[`scripts/validate-questionnaire.sh`](../scripts/validate-questionnaire.sh) — no custom IG
needed, since nothing is being constrained.

`QuestionnaireLinkIds` (C#) and `LINK` (`web/src/questionnaire/linkIds.ts`) are hand-kept
mirrors of the same linkId strings — the same relationship the dashboard's `terminology.ts`
already has with `Systems.cs`'s SNOMED/LOINC constants. A finite, rarely-changing set of
ids; the cost of drift is a compile-clean but silently-wrong field, caught immediately by
`QuestionnaireCatalogTests` (every linkId `QuestionnaireExtraction` reads is asserted
present on the built Questionnaire) and `QuestionnaireExtractionTests` (extraction is
exercised against realistic answers, not the catalog's own output, so the two can't drift
into agreement with each other and still be wrong).

## The field → resource map

| Questionnaire item (linkId) | Type | CaseSubmission field | FHIR resource.element |
| --- | --- | --- | --- |
| `patient` | group | `Patient` | — |
| `patient.nationalId` | string, regex `^\d{10,17}$` | `Patient.NationalId` | `Patient.identifier` (`http://health.gov.bd/sid`) |
| `patient.familyName` | string | `Patient.FamilyName` | `Patient.name.family` |
| `patient.givenNames` | string, repeats | `Patient.GivenNames` | `Patient.name.given` |
| `patient.gender` | choice (`administrative-gender`) | `Patient.Gender` | `Patient.gender` |
| `patient.birthDate` | date | `Patient.BirthDate` | `Patient.birthDate` |
| `patient.city` | string | `Patient.City` | `Patient.address.city` |
| `patient.district` | string | `Patient.District` | `Patient.address.district` |
| `disease` | choice, SNOMED (38362002 dengue / 61462000 malaria) | `Disease` | `Observation.code` (LOINC, via `CaseBundleBuilder.TestFor`) |
| `rdtResult` | choice, SNOMED (10828004 positive / 260385009 negative) | `RdtResult` | `Observation.valueCodeableConcept` |
| `visitDate` | dateTime | `VisitDate` | `Encounter.period`, `Observation.effective` |
| `diagnosisNote` | string, `enableWhen rdtResult = positive` | *(not extracted — a note, not a coded fact)* | — |

`disease` and `rdtResult`'s `answerOption`s carry the identical SNOMED codes
`CaseBundleBuilder` writes onto the Observation — extraction is a lookup
(`QuestionnaireExtraction.DiseaseOf`/`RdtResultOf`), not a translation table.

`diagnosisNote` is the one item with no `CaseSubmission` target: it exists to demonstrate
`enableWhen` (hidden until the RDT is positive — a diagnosis note is meaningless before
then) without inventing a resource-model field just to hold it. A real deployment would
route it to `Condition.note`; Prohori's Condition is auto-derived from `disease` + a
positive result (Phase C), so there is nowhere honest to put free text yet.

## ODK/KoBo equivalence

The community-health-worker forms this project models after are typically built in
ODK/KoBo XLSForm. The mapping below is what porting one of those forms to FHIR
`Questionnaire` looks like in practice:

| XLSForm | FHIR Questionnaire |
| --- | --- |
| `type: text`, `select_one`, `date`, `datetime` | `item.type`: `string`, `choice`, `date`, `dateTime` |
| `required: yes` | `item.required: true` |
| `choices` sheet (list_name, name, label) | `item.answerOption[].valueCoding` (code = `name`, display = `label`) — or `item.answerValueSet` for a large/shared list |
| `relevant: ${rdt_result}='positive'` | `item.enableWhen` (`question`, `operator`, `answer[x]`) |
| `begin group` / `end group` | `item.type: group` with nested `item[]` |
| form `id` + `version` | `Questionnaire.url` + `Questionnaire.version` |
| XLSForm submission (XML/JSON) | `QuestionnaireResponse` |

The practical gap: XLSForm choices are plain strings with no terminology binding; porting
to FHIR is the point where a project either binds them to a real code system (as done here
— SNOMED for disease/result) or accepts an `open-choice`/`string` item and defers coding to
extraction (Phase K's terminology work is the natural next step for a larger choice list).

## `$populate`

`POST /questionnaire-response/$populate` (body `{"nationalId": "..."}`) searches for an
existing `Patient` by National ID and returns a `QuestionnaireResponse` with the
`patient` group's answers filled in — or an empty, `in-progress` response if none matches
(never an error: a not-found National ID usually just means a new patient). This is a
deliberate simplification of the SDC `$populate` operation, which formally takes a
`Parameters` resource (`questionnaire`, `subject`, etc.); a bare `{nationalId}` trigger
carries the same intent — "prefill from what we already know about this person" — without
requiring the caller to construct a `Parameters` wrapper for a single input value. Noted
here rather than left silent, in the same spirit as the BD-Core `bd-condition` workaround
(Phase F) and bulk staying disabled-by-default (Phase I).

## `$extract`

`POST /questionnaire-response/$extract?dryRun=true|false` takes a filled-in
`QuestionnaireResponse`, walks it into a `CaseSubmission` (`QuestionnaireExtraction.Extract`),
and — unless `dryRun`— submits the resulting Bundle exactly as `POST /cases` would, through
the same `FhirCaseService.SubmitAsync(Bundle)`. A missing or malformed required answer comes
back as the same `{errors: {linkId: [...]}}` shape `MiniValidator` produces for the typed
endpoint (`Results.ValidationProblem`), keyed by linkId instead of property name.

Because extraction hands off to the exact `CaseBundleBuilder` already exercised end-to-end
against a live FHIR server in `CaseSubmissionIntegrationTests` (Phase C), that live-server
coverage carries over transitively — there is no second Bundle-shape to separately prove
submittable. `QuestionnaireExtractionTests.The_extracted_submission_builds_the_same_Bundle_as_a_typed_submission`
asserts the two paths converge.

## The dashboard's New Case form

`web/src/pages/NewCase.tsx` fetches the live Questionnaire (`useCaseQuestionnaire`, cached —
it changes only on deploy) and renders it with `QuestionnaireForm`, which walks
`Questionnaire.item[]` **generically**: one recursive component handles `group`, `string`,
`choice`, `date` and `dateTime` by `item.type`, not by linkId. Nothing about the "shape" of
a Prohori case — which fields exist, what's required, when `diagnosisNote` should show — is
hardcoded in the renderer; it all comes from the fetched resource. `enableWhen` is
evaluated client-side against the current in-progress answers before every render, the same
condition the server independently tolerates (an unanswered `diagnosisNote` on a negative
result is not an extraction error).

Submitting builds a `QuestionnaireResponse` — mirroring the same item tree, so the answer
shape always matches what was rendered — and posts it to `$extract`. Both `$populate` and
`$extract` need a write-scoped bearer token (`CaseWrite`, same policy as `/cases` and
`/cases/{id}` since Phase H); the page shows a SMART-launch prompt instead of a broken form
when no token is present, rather than letting the request fail with a bare 401.
