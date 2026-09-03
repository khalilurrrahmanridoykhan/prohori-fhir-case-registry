import { FHIR_BASE } from "../config";
import type { Bundle, FhirResource } from "./r4";

type Params = Record<string, string | string[]>;

/** GET [base]/<path>?<params> — for search interactions. */
export async function fhirSearch(path: string, params: Params = {}): Promise<Bundle> {
  const url = new URL(FHIR_BASE + path);
  for (const [key, value] of Object.entries(params)) {
    for (const v of Array.isArray(value) ? value : [value]) url.searchParams.append(key, v);
  }
  return request(url.toString());
}

/** GET [base]/<path> verbatim — for operations like Patient/{id}/$everything. */
export async function fhirGet(path: string): Promise<Bundle> {
  return request(FHIR_BASE + path);
}

async function request(url: string): Promise<Bundle> {
  const res = await fetch(url, { headers: { Accept: "application/fhir+json" } });
  if (!res.ok) {
    throw new Error(`FHIR ${res.status} ${res.statusText} — ${url}`);
  }
  return res.json() as Promise<Bundle>;
}

/** "Patient/123" | "Patient/123/_history/1" | "urn:uuid:abc" -> "123" | "abc" */
export function referenceId(reference?: string): string | undefined {
  if (!reference) return undefined;
  if (reference.startsWith("urn:uuid:")) return reference.slice("urn:uuid:".length);
  const match = reference.match(/([A-Za-z]+)\/([^/]+?)(?:\/_history\/.*)?$/);
  return match?.[2];
}

export function bundleResources(bundle: Bundle): FhirResource[] {
  return (bundle.entry ?? [])
    .map((e) => e.resource)
    .filter((r): r is FhirResource => Boolean(r));
}
