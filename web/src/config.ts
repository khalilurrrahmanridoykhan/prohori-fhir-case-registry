export const FHIR_BASE = (
  import.meta.env.VITE_FHIR_BASE ?? "https://hapi.fhir.org/baseR4"
).replace(/\/$/, "");

export const API_BASE = (
  import.meta.env.VITE_API_BASE ?? "http://localhost:5279"
).replace(/\/$/, "");

/** Prohori.Subscriber (Phase O) — SSE stream for live dashboard updates. Local-only,
 * like API_BASE: the public Vercel deploy has no reachable Subscriber, so the live-update
 * hook degrades to "unavailable" rather than erroring — see useLiveUpdates. */
export const SUBSCRIBER_BASE = (
  import.meta.env.VITE_SUBSCRIBER_BASE ?? "http://localhost:5300"
).replace(/\/$/, "");

export const NATIONAL_ID_SYSTEM = "http://health.gov.bd/sid";

/** Every Prohori resource carries this tag; the dashboard only ever shows tagged data. */
export const COHORT_TAG = "urn:prohori|demo-cohort";
