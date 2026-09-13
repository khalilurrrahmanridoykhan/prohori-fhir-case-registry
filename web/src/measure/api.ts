import { useQuery } from "@tanstack/react-query";
import { API_BASE } from "../config";

export interface CityCounts {
  city: string;
  denominator: number;
  numerator: number;
}

export interface MeasureCounts {
  denominator: number;
  numerator: number;
  byCity: CityCounts[];
}

interface PopulationJson {
  code?: { coding?: { code?: string }[] };
  count?: number;
}

interface MeasureReportJson {
  group?: {
    population?: PopulationJson[];
    stratifier?: {
      stratum?: { value?: { text?: string }; population?: PopulationJson[] }[];
    }[];
  }[];
}

function countFor(populations: PopulationJson[], code: string): number {
  return populations.find((p) => p.code?.coding?.[0]?.code === code)?.count ?? 0;
}

function parse(report: MeasureReportJson): MeasureCounts {
  const group = report.group?.[0];
  const population = group?.population ?? [];
  const byCity = (group?.stratifier ?? []).flatMap((s) =>
    (s.stratum ?? []).map((st) => ({
      city: st.value?.text ?? "Unknown",
      denominator: countFor(st.population ?? [], "denominator"),
      numerator: countFor(st.population ?? [], "numerator"),
    })),
  );
  return { denominator: countFor(population, "denominator"), numerator: countFor(population, "numerator"), byCity };
}

/**
 * $evaluate-measure over [periodStart, periodEnd] — disabled while either bound is missing.
 * Fails fast and quietly (no retry): the live dashboard has no API to reach, and callers are
 * expected to fall back to client-side counting rather than show an error for that.
 */
export function useMeasureReport(periodStart: string | undefined, periodEnd: string | undefined) {
  return useQuery({
    queryKey: ["measure-report", periodStart, periodEnd],
    enabled: Boolean(periodStart && periodEnd),
    retry: false,
    staleTime: 60_000,
    queryFn: async (): Promise<MeasureCounts> => {
      const url = `${API_BASE}/measure/$evaluate-measure?periodStart=${periodStart}&periodEnd=${periodEnd}`;
      const res = await fetch(url, { headers: { Accept: "application/fhir+json" } });
      if (!res.ok) throw new Error(`measure ${res.status}`);
      return parse((await res.json()) as MeasureReportJson);
    },
  });
}
