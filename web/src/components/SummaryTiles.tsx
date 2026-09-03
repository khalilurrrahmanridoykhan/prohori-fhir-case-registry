import type { CaseRow } from "../fhir/cases";

const DAY = 86_400_000;

export function SummaryTiles({ cases }: { cases: CaseRow[] }) {
  const total = cases.length;
  const positive = cases.filter((c) => c.result === "positive").length;
  const positivity = total ? Math.round((positive / total) * 100) : 0;

  const now = Date.now();
  const lastWeek = cases.filter((c) => {
    const t = Date.parse(c.visitDate);
    return !Number.isNaN(t) && now - t <= 7 * DAY;
  }).length;

  const dengue = cases.filter((c) => c.disease === "dengue").length;
  const malaria = cases.filter((c) => c.disease === "malaria").length;

  return (
    <section className="tiles" aria-label="Summary">
      <div className="tile">
        <div className="tile__label">Cases</div>
        <div className="tile__value">{total}</div>
        <div className="tile__sub">{lastWeek} in the last 7 days</div>
      </div>
      <div className="tile">
        <div className="tile__label">Positivity</div>
        <div className={`tile__value ${positive ? "tile__value--alert" : ""}`}>{positivity}%</div>
        <div className="tile__sub">
          {positive} positive of {total}
        </div>
      </div>
      <div className="tile">
        <div className="tile__label">Dengue</div>
        <div className="tile__value">{dengue}</div>
        <div className="tile__sub">
          {cases.filter((c) => c.disease === "dengue" && c.result === "positive").length} confirmed
        </div>
      </div>
      <div className="tile">
        <div className="tile__label">Malaria</div>
        <div className="tile__value">{malaria}</div>
        <div className="tile__sub">
          {cases.filter((c) => c.disease === "malaria" && c.result === "positive").length} confirmed
        </div>
      </div>
    </section>
  );
}
