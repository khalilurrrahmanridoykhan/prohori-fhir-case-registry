import { useMemo, useState } from "react";
import { CaseTable } from "../components/CaseTable";
import { EMPTY_FILTERS, Filters, type CaseFilters } from "../components/Filters";
import { SummaryTiles } from "../components/SummaryTiles";
import { useCases } from "../fhir/cases";

export function Dashboard() {
  const { data: cases, isLoading, isError, error } = useCases();
  const [filters, setFilters] = useState<CaseFilters>(EMPTY_FILTERS);

  const cities = useMemo(
    () => [...new Set((cases ?? []).map((c) => c.city).filter((c) => c && c !== "—"))].sort(),
    [cases],
  );

  const filtered = useMemo(() => {
    return (cases ?? []).filter((c) => {
      if (filters.disease && c.disease !== filters.disease) return false;
      if (filters.result && c.result !== filters.result) return false;
      if (filters.city && c.city !== filters.city) return false;
      if (filters.from && c.visitDate.slice(0, 10) < filters.from) return false;
      return true;
    });
  }, [cases, filters]);

  return (
    <>
      <h1 className="page__title">Case surveillance</h1>
      <p className="page__lede">
        Field-visit RDT results and diagnoses for dengue and malaria. Click a row for the patient
        timeline.
      </p>

      {isLoading && <div className="state">Loading cases…</div>}

      {isError && (
        <div className="state state--error">
          Could not load cases — {(error as Error).message}
        </div>
      )}

      {cases && cases.length === 0 && (
        <div className="state">
          No cases on this server yet. Seed some:
          <br />
          <code>python3 scripts/seed-cohort.py</code> &nbsp;or&nbsp; <code>POST /cases</code> via the API.
        </div>
      )}

      {cases && cases.length > 0 && (
        <>
          <SummaryTiles cases={filtered} />
          <Filters
            filters={filters}
            cities={cities}
            shown={filtered.length}
            total={cases.length}
            onChange={setFilters}
          />
          {filtered.length > 0 ? (
            <CaseTable rows={filtered} />
          ) : (
            <div className="state">No cases match these filters.</div>
          )}
        </>
      )}
    </>
  );
}
