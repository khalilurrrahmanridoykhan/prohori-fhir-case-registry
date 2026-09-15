import { useQuery } from "@tanstack/react-query";
import { bundleResources, fhirSearch } from "./client";
import type { AuditEvent } from "./r4";

export interface AuditEntry {
  id: string;
  recorded: string;
  agent: string;
  breakGlass: boolean;
}

function agentDisplay(ev: AuditEvent): string {
  return ev.agent?.[0]?.who?.display ?? "unknown";
}

function isBreakGlass(ev: AuditEvent): boolean {
  return (ev.agent ?? []).some((a) =>
    (a.purposeOfUse ?? []).some((p) => p.coding?.some((c) => c.code === "BTG")),
  );
}

/**
 * Every AuditEvent naming this patient — FhirCaseService adds one to the same
 * transaction as every case write (Phase O), so this is a real who-did-what trail,
 * not a client-side log.
 */
export function useAuditTrail(patientId: string | undefined) {
  return useQuery({
    queryKey: ["audit-trail", patientId],
    enabled: Boolean(patientId),
    queryFn: async (): Promise<AuditEntry[]> => {
      const bundle = await fhirSearch("/AuditEvent", {
        entity: `Patient/${patientId}`,
        _sort: "-date",
        _count: "50",
      });
      return bundleResources(bundle)
        .filter((r): r is AuditEvent => r.resourceType === "AuditEvent")
        .map((ev) => ({
          id: ev.id ?? "",
          recorded: ev.recorded ?? "",
          agent: agentDisplay(ev),
          breakGlass: isBreakGlass(ev),
        }));
    },
  });
}
