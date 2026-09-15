import type { LiveStatus } from "../realtime/useLiveUpdates";

const LABEL: Record<LiveStatus, string> = {
  connecting: "Connecting…",
  live: "Live",
  unavailable: "Live updates unavailable",
};

/** Only rendered once we know something — skips a flash of "connecting" on the public
 * read-only deploy, where the Subscriber is never reachable and every load ends the
 * same way regardless of how long it's shown. */
export function LiveBadge({ status }: { status: LiveStatus }) {
  if (status === "unavailable") return null;

  return (
    <span className={`live-badge live-badge--${status}`} title="Prohori.Subscriber (Phase O)">
      <span className="live-badge__dot" aria-hidden="true" />
      {LABEL[status]}
    </span>
  );
}
