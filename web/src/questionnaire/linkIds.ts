// Mirrors src/Prohori.Api/Fhir/QuestionnaireCatalog.cs's QuestionnaireLinkIds — the same relationship
// terminology.ts already has with Systems.cs: stable string ids, hand-kept in sync, low drift risk.
export const LINK = {
  patient: "patient",
  nationalId: "patient.nationalId",
  familyName: "patient.familyName",
  givenNames: "patient.givenNames",
  gender: "patient.gender",
  birthDate: "patient.birthDate",
  city: "patient.city",
  district: "patient.district",
  disease: "disease",
  rdtResult: "rdtResult",
  visitDate: "visitDate",
  diagnosisNote: "diagnosisNote",
} as const;
