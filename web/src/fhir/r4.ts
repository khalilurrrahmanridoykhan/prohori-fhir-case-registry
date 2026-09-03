// Re-export just the FHIR R4 resource types Prohori touches, from @types/fhir.
export type {
  Bundle,
  CodeableConcept,
  Condition,
  Encounter,
  FhirResource,
  Observation,
  Patient,
} from "fhir/r4";
