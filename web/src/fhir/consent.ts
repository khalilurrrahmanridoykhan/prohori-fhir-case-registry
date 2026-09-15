import { useQuery, useQueryClient } from "@tanstack/react-query";
import { API_BASE, NATIONAL_ID_SYSTEM } from "../config";
import { accessToken } from "../smart";
import { bundleResources, fhirSearch } from "./client";
import type { Consent } from "./r4";

export type ConsentProvision = "permit" | "deny";

/** Read-only: the dashboard reads FHIR directly for this, same as everything else it
 * displays — only the toggle itself needs the authenticated API. */
export function useConsentStatus(nationalId: string | undefined) {
  return useQuery({
    queryKey: ["consent", nationalId],
    enabled: Boolean(nationalId),
    // null, not undefined, for "no Consent found" — a queryFn returning undefined is a
    // TanStack Query anti-pattern (it logs a console error: that value is reserved to mean
    // "no data yet", not "resolved to nothing").
    queryFn: async (): Promise<ConsentProvision | null> => {
      const bundle = await fhirSearch("/Consent", {
        identifier: `${NATIONAL_ID_SYSTEM}|${nationalId}`,
        _count: "1",
      });
      const consent = bundleResources(bundle).find((r): r is Consent => r.resourceType === "Consent");
      if (!consent) return null;
      return consent.provision?.type === "deny" ? "deny" : "permit";
    },
  });
}

/** PUT /patients/{nationalId}/consent — needs the same write-scoped SMART launch as
 * submitting a case; see NewCase's identical pattern. */
export function useToggleConsent(nationalId: string | undefined) {
  const queryClient = useQueryClient();

  return async (provision: ConsentProvision) => {
    if (!nationalId) return;
    const token = accessToken();
    if (!token) throw new Error("SMART launch required — no access token.");

    const response = await fetch(`${API_BASE}/patients/${nationalId}/consent`, {
      method: "PUT",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
      body: JSON.stringify({ provision }),
    });
    if (!response.ok) {
      const problem = await response.json().catch(() => undefined);
      throw new Error(problem?.error ?? problem?.detail ?? `${response.status} ${response.statusText}`);
    }
    queryClient.invalidateQueries({ queryKey: ["consent", nationalId] });
  };
}
