import { useAuditTrail } from "../fhir/audit";

const fmtDateTime = (iso: string) => {
  const t = Date.parse(iso);
  return Number.isNaN(t)
    ? "—"
    : new Date(t).toLocaleString(undefined, {
        year: "numeric",
        month: "short",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
      });
};

/** Who created or read this patient's record, and when — one AuditEvent per write
 * (Phase O), plus any break-glass read of a consent-denied record. */
export function AuditTrail({ patientId }: { patientId: string | undefined }) {
  const { data, isSuccess } = useAuditTrail(patientId);

  if (!isSuccess || data.length === 0) return null;

  return (
    <section className="audit-trail">
      <h3 className="page__title" style={{ fontSize: 16 }}>
        Audit trail
      </h3>
      <ul className="audit-trail__list">
        {data.map((entry) => (
          <li
            key={entry.id}
            className={`audit-trail__item ${entry.breakGlass ? "audit-trail__item--break-glass" : ""}`}
          >
            <span className="audit-trail__date">{fmtDateTime(entry.recorded)}</span>
            <span className="audit-trail__agent">{entry.agent}</span>
            {entry.breakGlass && <span>— break-glass override</span>}
          </li>
        ))}
      </ul>
    </section>
  );
}
