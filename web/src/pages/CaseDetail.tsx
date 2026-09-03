import { Link, useParams } from "react-router-dom";
import { useCaseTimeline } from "../fhir/cases";
import type { Patient } from "../fhir/r4";

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

function patientName(patient?: Patient): string {
  const n = patient?.name?.[0];
  if (!n) return "Unknown patient";
  return n.text ?? ([n.given?.join(" "), n.family].filter(Boolean).join(" ") || "Unknown patient");
}

export function CaseDetail() {
  const { patientId } = useParams<{ patientId: string }>();
  const { data, isLoading, isError, error } = useCaseTimeline(patientId);

  const patient = data?.patient;
  const nid = patient?.identifier?.find((i) => i.system === "http://health.gov.bd/sid")?.value;
  const addr = patient?.address?.[0];

  return (
    <>
      <Link to="/" className="backlink">
        ← All cases
      </Link>

      {isLoading && <div className="state">Loading patient record…</div>}
      {isError && (
        <div className="state state--error">Could not load record — {(error as Error).message}</div>
      )}

      {data && (
        <>
          <section className="patient-card">
            <h2>{patientName(patient)}</h2>
            <dl>
              <div>
                <dt>National ID</dt>
                <dd>{nid ?? "—"}</dd>
              </div>
              <div>
                <dt>Sex</dt>
                <dd>{patient?.gender ?? "—"}</dd>
              </div>
              <div>
                <dt>Born</dt>
                <dd>{patient?.birthDate ?? "—"}</dd>
              </div>
              <div>
                <dt>Location</dt>
                <dd>{[addr?.city, addr?.district].filter(Boolean).join(", ") || "—"}</dd>
              </div>
              <div>
                <dt>FHIR id</dt>
                <dd>{patient?.id}</dd>
              </div>
            </dl>
          </section>

          <h3 className="page__title" style={{ fontSize: 16 }}>
            Timeline
          </h3>
          {data.events.length === 0 ? (
            <div className="state">No recorded events.</div>
          ) : (
            <ol className="timeline">
              {data.events.map((ev) => (
                <li key={`${ev.kind}-${ev.id}`} className={`event event--${ev.kind}`}>
                  <div className="event__meta">
                    {ev.kind} · <span className="event__date">{fmtDateTime(ev.date)}</span>
                  </div>
                  <div className="event__title">{ev.title}</div>
                  {ev.detail && <div className="event__detail">{ev.detail}</div>}
                </li>
              ))}
            </ol>
          )}
        </>
      )}
    </>
  );
}
