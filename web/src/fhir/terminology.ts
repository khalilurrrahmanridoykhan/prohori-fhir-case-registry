import type { CodeableConcept } from "./r4";

export type Disease = "dengue" | "malaria" | "unknown";
export type Result = "positive" | "negative" | "unknown";

export const LOINC = "http://loinc.org";
export const SNOMED = "http://snomed.info/sct";

const LOINC_DISEASE: Record<string, Disease> = {
  "42239-4": "dengue", // Dengue virus NS1 Ag
  "70048-1": "malaria", // Plasmodium sp Ag, rapid immunoassay
};

const SNOMED_RESULT: Record<string, Result> = {
  "10828004": "positive",
  "260385009": "negative",
};

export const diseaseFromLoinc = (code?: string): Disease =>
  (code && LOINC_DISEASE[code]) || "unknown";

export const resultFromSnomed = (code?: string): Result =>
  (code && SNOMED_RESULT[code]) || "unknown";

export const diseaseLabel: Record<Disease, string> = {
  dengue: "Dengue",
  malaria: "Malaria",
  unknown: "Unknown",
};

export function codingText(concept?: CodeableConcept): string | undefined {
  if (!concept) return undefined;
  return (
    concept.text ??
    concept.coding?.find((c) => c.display)?.display ??
    concept.coding?.[0]?.code
  );
}

export function codeFrom(concept: CodeableConcept | undefined, system: string): string | undefined {
  return concept?.coding?.find((c) => c.system === system)?.code;
}
