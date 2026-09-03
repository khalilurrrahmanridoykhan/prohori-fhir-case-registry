export interface CaseFilters {
  disease: string;
  result: string;
  city: string;
  from: string;
}

export const EMPTY_FILTERS: CaseFilters = { disease: "", result: "", city: "", from: "" };

export function Filters({
  filters,
  cities,
  shown,
  total,
  onChange,
}: {
  filters: CaseFilters;
  cities: string[];
  shown: number;
  total: number;
  onChange: (next: CaseFilters) => void;
}) {
  const set = (patch: Partial<CaseFilters>) => onChange({ ...filters, ...patch });
  const dirty = JSON.stringify(filters) !== JSON.stringify(EMPTY_FILTERS);

  return (
    <div className="filters">
      <div className="field">
        <label htmlFor="f-disease">Disease</label>
        <select id="f-disease" value={filters.disease} onChange={(e) => set({ disease: e.target.value })}>
          <option value="">All</option>
          <option value="dengue">Dengue</option>
          <option value="malaria">Malaria</option>
        </select>
      </div>

      <div className="field">
        <label htmlFor="f-result">RDT result</label>
        <select id="f-result" value={filters.result} onChange={(e) => set({ result: e.target.value })}>
          <option value="">All</option>
          <option value="positive">Positive</option>
          <option value="negative">Negative</option>
        </select>
      </div>

      <div className="field">
        <label htmlFor="f-city">City</label>
        <select id="f-city" value={filters.city} onChange={(e) => set({ city: e.target.value })}>
          <option value="">All</option>
          {cities.map((c) => (
            <option key={c} value={c}>
              {c}
            </option>
          ))}
        </select>
      </div>

      <div className="field">
        <label htmlFor="f-from">Visits since</label>
        <input
          id="f-from"
          type="date"
          value={filters.from}
          onChange={(e) => set({ from: e.target.value })}
        />
      </div>

      {dirty && (
        <button type="button" className="filters__reset" onClick={() => onChange(EMPTY_FILTERS)}>
          Reset
        </button>
      )}

      <span className="filters__count">
        {shown} / {total}
      </span>
    </div>
  );
}
