// Re-export just the FHIR R4 resource types Prohori touches, from @types/fhir.
export type {
  Bundle,
  CodeableConcept,
  Coding,
  Condition,
  Encounter,
  FhirResource,
  Observation,
  Patient,
  Questionnaire,
  QuestionnaireItem,
  QuestionnaireResponse,
  QuestionnaireResponseItem,
  QuestionnaireResponseItemAnswer,
} from "fhir/r4";
