import { useQuery } from "@tanstack/react-query";
import { COHORT_TAG } from "../config";
import { bundleResources, fhirGet, fhirSearch, referenceId } from "./client";
import type { Condition, Encounter, Observation, Patient } from "./r4";
import {
  codeFrom,
  codingText,
  diseaseFromLoinc,
  LOINC,
  resultFromSnomed,
  SNOMED,
  type Disease,
  type Result,
} from "./terminology";

export interface CaseRow {
  encounterId: string;
  patientId: string;
  patientName: string;
  city: string;
  visitDate: string; // ISO 8601
  disease: Disease;
  result: Result;
  diagnosis?: string;
}

function humanName(patient?: Patient): string {
  const name = patient?.name?.[0];
  if (!name) return "Unknown";
  return name.text ?? ([name.given?.join(" "), name.family].filter(Boolean).join(" ") || "Unknown");
}

function groupBy<T>(items: T[], key: (item: T) => string | undefined): Map<string, T[]> {
  const map = new Map<string, T[]>();
  for (const item of items) {
    const k = key(item);
    if (!k) continue;
    const bucket = map.get(k) ?? [];
    bucket.push(item);
    map.set(k, bucket);
  }
  return map;
}

/**
 * One query, one row per visit:
 *   Encounter + its Patient (_include) + its Observation & Condition (_revinclude)
 */
export function useCases() {
  return useQuery({
    queryKey: ["cases"],
    queryFn: async (): Promise<CaseRow[]> => {
      const bundle = await fhirSearch("/Encounter", {
        _tag: COHORT_TAG,
        _include: "Encounter:subject",
        _revinclude: ["Observation:encounter", "Condition:encounter"],
        _sort: "-date",
        _count: "300",
      });

      const resources = bundleResources(bundle);
      const patients = new Map(
        resources
          .filter((r): r is Patient => r.resourceType === "Patient")
          .map((p) => [p.id!, p]),
      );
      const observations = groupBy(
        resources.filter((r): r is Observation => r.resourceType === "Observation"),
        (o) => referenceId(o.encounter?.reference),
      );
      const conditions = groupBy(
        resources.filter((r): r is Condition => r.resourceType === "Condition"),
        (c) => referenceId(c.encounter?.reference),
      );

      return resources
        .filter((r): r is Encounter => r.resourceType === "Encounter")
        .map((enc): CaseRow => {
          const patientId = referenceId(enc.subject?.reference);
          const patient = patientId ? patients.get(patientId) : undefined;
          const obs = observations.get(enc.id!)?.[0];
          const cond = conditions.get(enc.id!)?.[0];

          return {
            encounterId: enc.id!,
            patientId: patientId ?? "",
            patientName: humanName(patient),
            city: patient?.address?.[0]?.city ?? "—",
            visitDate: enc.period?.start ?? obs?.effectiveDateTime ?? "",
            disease: diseaseFromLoinc(codeFrom(obs?.code, LOINC)),
            result: resultFromSnomed(codeFrom(obs?.valueCodeableConcept, SNOMED)),
            diagnosis: codingText(cond?.code),
          };
        })
        .filter((row) => row.patientId)
        .sort((a, b) => b.visitDate.localeCompare(a.visitDate));
    },
  });
}

export interface TimelineEvent {
  id: string;
  kind: "Encounter" | "Observation" | "Condition";
  date: string;
  title: string;
  detail?: string;
}

/** Patient/{id}/$everything → a chronological list of what happened. */
export function useCaseTimeline(patientId: string | undefined) {
  return useQuery({
    queryKey: ["timeline", patientId],
    enabled: Boolean(patientId),
    queryFn: async () => {
      const bundle = await fhirGet(`/Patient/${patientId}/$everything?_count=200`);
      const resources = bundleResources(bundle);
      const patient = resources.find((r): r is Patient => r.resourceType === "Patient");

      const events: TimelineEvent[] = [];
      for (const r of resources) {
        if (r.resourceType === "Encounter") {
          events.push({
            id: r.id!,
            kind: "Encounter",
            date: r.period?.start ?? "",
            title: "Field visit",
            detail: r.class?.display ?? r.class?.code,
          });
        } else if (r.resourceType === "Observation") {
          events.push({
            id: r.id!,
            kind: "Observation",
            date: r.effectiveDateTime ?? "",
            title: codingText(r.code) ?? "Observation",
            detail: codingText(r.valueCodeableConcept),
          });
        } else if (r.resourceType === "Condition") {
          events.push({
            id: r.id!,
            kind: "Condition",
            date: r.recordedDate ?? r.onsetDateTime ?? "",
            title: codingText(r.code) ?? "Condition",
            detail: [codingText(r.clinicalStatus), codingText(r.verificationStatus)]
              .filter(Boolean)
              .join(" · "),
          });
        }
      }
      events.sort((a, b) => a.date.localeCompare(b.date));

      return { patient, events };
    },
  });
}
