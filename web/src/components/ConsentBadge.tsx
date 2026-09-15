import { useState } from "react";
import { useConsentStatus, useToggleConsent } from "../fhir/consent";
import { accessToken } from "../smart";

/** Shows the patient's current Consent (default: permit, set with their first case) and,
 * with a write-scoped SMART launch, a button to flip it — deny then makes the API's
 * GET /patients/{nationalId} refuse with 403. Read-only (no SMART launch) on the public
 * Vercel deploy, same as everywhere else write access is gated in this dashboard. */
export function ConsentBadge({ nationalId }: { nationalId: string | undefined }) {
  const { data: status, isSuccess } = useConsentStatus(nationalId);
  const toggle = useToggleConsent(nationalId);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();

  if (!isSuccess || !status) return null;

  const canWrite = Boolean(accessToken());
  const next = status === "permit" ? "deny" : "permit";

  async function handleToggle() {
    setBusy(true);
    setError(undefined);
    try {
      await toggle(next);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="consent-badge">
      <span className={`consent-badge__state consent-badge__state--${status}`}>
        Consent: {status}
      </span>
      {canWrite && (
        <button type="button" className="consent-badge__toggle" onClick={handleToggle} disabled={busy}>
          {busy ? "Updating…" : `Set to ${next}`}
        </button>
      )}
      {error && <span className="state state--error">{error}</span>}
    </div>
  );
}
