import { useNavigate } from "react-router-dom";
import type { CaseRow } from "../fhir/cases";
import { DiseaseTag, ResultPill } from "./Pills";

const fmtDate = (iso: string) => {
  const t = Date.parse(iso);
  return Number.isNaN(t)
    ? "—"
    : new Date(t).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "2-digit" });
};

export function CaseTable({ rows }: { rows: CaseRow[] }) {
  const navigate = useNavigate();

  return (
    <div className="table-wrap">
      <table className="cases">
        <thead>
          <tr>
            <th>Patient</th>
            <th>City</th>
            <th>Visit</th>
            <th>Disease</th>
            <th>RDT</th>
            <th>Diagnosis</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr
              key={row.encounterId}
              onClick={() => navigate(`/cases/${row.patientId}`)}
              tabIndex={0}
              onKeyDown={(e) => e.key === "Enter" && navigate(`/cases/${row.patientId}`)}
            >
              <td>
                <div className="name">{row.patientName}</div>
                <div className="muted">{row.patientId}</div>
              </td>
              <td>{row.city}</td>
              <td className="date">{fmtDate(row.visitDate)}</td>
              <td>
                <DiseaseTag disease={row.disease} />
              </td>
              <td>
                <ResultPill result={row.result} />
              </td>
              <td>{row.diagnosis ?? <span className="muted">—</span>}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
