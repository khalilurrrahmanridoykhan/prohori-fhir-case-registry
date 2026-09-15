// Re-export just the FHIR R4 resource types Prohori touches, from @types/fhir.
export type {
  AuditEvent,
  Bundle,
  CodeableConcept,
  Coding,
  Condition,
  Consent,
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
