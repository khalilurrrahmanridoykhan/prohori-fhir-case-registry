export const FHIR_BASE = (
  import.meta.env.VITE_FHIR_BASE ?? "https://hapi.fhir.org/baseR4"
).replace(/\/$/, "");

export const API_BASE = (
  import.meta.env.VITE_API_BASE ?? "http://localhost:5279"
).replace(/\/$/, "");

/** Every Prohori resource carries this tag; the dashboard only ever shows tagged data. */
export const COHORT_TAG = "urn:prohori|demo-cohort";
