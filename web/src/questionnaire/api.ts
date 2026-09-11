import { useQuery } from "@tanstack/react-query";
import { API_BASE } from "../config";
import { accessToken } from "../smart";
import type { Questionnaire, QuestionnaireResponse } from "../fhir/r4";

function authHeaders(extra?: Record<string, string>): HeadersInit {
  const token = accessToken();
  if (!token) throw new Error("SMART launch required — no access token.");
  return { Authorization: `Bearer ${token}`, ...extra };
}

async function readFhirJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const problem = await response.json().catch(() => undefined);
    const detail = problem?.detail ?? problem?.title ?? problem?.error
      ?? (problem?.errors ? Object.entries(problem.errors).map(([k, v]) => `${k}: ${(v as string[]).join(", ")}`).join("; ") : undefined);
    throw new Error(detail ?? `${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

/** GET /questionnaire-response/questionnaire — the field-intake form definition. Public, no auth. */
export function useCaseQuestionnaire() {
  return useQuery({
    queryKey: ["questionnaire"],
    queryFn: async () => readFhirJson<Questionnaire>(
      await fetch(`${API_BASE}/questionnaire-response/questionnaire`, { headers: { Accept: "application/fhir+json" } }),
    ),
    staleTime: Infinity,
  });
}

/** POST /questionnaire-response/$populate — a blank or NID-prefilled response for a returning patient. */
export async function populate(nationalId: string): Promise<QuestionnaireResponse> {
  const response = await fetch(`${API_BASE}/questionnaire-response/$populate`, {
    method: "POST",
    headers: authHeaders({ "Content-Type": "application/json" }),
    body: JSON.stringify({ nationalId }),
  });
  return readFhirJson<QuestionnaireResponse>(response);
}

/** POST /questionnaire-response/$extract — build (and, unless dryRun, submit) the case Bundle. */
export async function extract(response: QuestionnaireResponse, dryRun = false): Promise<{ created: string[] }> {
  const result = await fetch(`${API_BASE}/questionnaire-response/$extract?dryRun=${dryRun}`, {
    method: "POST",
    headers: authHeaders({ "Content-Type": "application/fhir+json" }),
    body: JSON.stringify(response),
  });
  return readFhirJson<{ created: string[] }>(result);
}
