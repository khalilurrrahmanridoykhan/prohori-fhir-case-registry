import { useEffect, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { SUBSCRIBER_BASE } from "../config";

export type LiveStatus = "connecting" | "live" | "unavailable";

/**
 * Subscribes to Prohori.Subscriber's SSE stream and invalidates the case list on every
 * event, so a new visit shows up without a manual reload — the Phase O "done when" gate.
 * Local-only: the public Vercel dashboard has no reachable Subscriber, so a failed/refused
 * connection just settles on "unavailable" rather than retrying forever or showing an error
 * — same degrade-invisibly shape as useMeasureReport (Phase M) and the New Case form
 * (Phase J).
 */
export function useLiveUpdates(): LiveStatus {
  const [status, setStatus] = useState<LiveStatus>("connecting");
  const queryClient = useQueryClient();

  useEffect(() => {
    let cancelled = false;
    const source = new EventSource(`${SUBSCRIBER_BASE}/stream`);

    source.onopen = () => {
      if (!cancelled) setStatus("live");
    };
    source.onmessage = () => {
      queryClient.invalidateQueries({ queryKey: ["cases"] });
    };
    source.onerror = () => {
      if (!cancelled) setStatus("unavailable");
    };

    return () => {
      cancelled = true;
      source.close();
    };
  }, [queryClient]);

  return status;
}
