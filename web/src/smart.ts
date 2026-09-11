import { API_BASE } from "./config";
import FHIR from 'fhirclient';
import type Client from 'fhirclient/lib/Client';

export let smartClient: Client | undefined;

/** The bearer token for calling Prohori.Api directly (writes never go through fhirclient's FHIR-only request()). */
export function accessToken(): string | undefined {
  return smartClient?.state.tokenResponse?.access_token;
}
export async function initializeSmart() {
  const params = new URLSearchParams(location.search);
  if (params.has('code') || params.has('state') || sessionStorage.getItem('prohori-smart')) {
    smartClient = await FHIR.oauth2.ready();
    if (!smartClient.patient.id && smartClient.state.serverUrl.replace(/\/$/, '') === `${API_BASE}/fhir`) {
      const response = await fetch(`${API_BASE}/smart/context`, { headers: { Authorization: `Bearer ${smartClient.state.tokenResponse?.access_token}` } });
      if (!response.ok) throw new Error(`Patient context refused (${response.status}).`);
      const context = await response.json() as { patient?: string };
      smartClient.state.tokenResponse = { ...smartClient.state.tokenResponse, patient: context.patient };
    }
    if (!smartClient.patient.id) throw new Error('Launch did not provide patient context. Relaunch with launch/patient.');
    await smartClient.patient.read();
    sessionStorage.setItem('prohori-smart', '1');
    history.replaceState(null, '', location.pathname);
  }
}
export function launchSmart() {
  const iss = new URLSearchParams(location.search).get('iss') ?? import.meta.env.VITE_SMART_ISS ?? 'https://launch.smarthealthit.org/v/r4/fhir';
  const ehr = new URLSearchParams(location.search).has('launch');
  return FHIR.oauth2.authorize({
    clientId: import.meta.env.VITE_SMART_CLIENT_ID ?? 'prohori-dashboard',
    scope: `${ehr ? 'launch ' : ''}launch/patient openid fhirUser patient/*.rs${iss === `${API_BASE}/fhir` ? ' user/*.write' : ''}`,
    redirectUri: new URL('/', location.href).href,
    iss,
    pkceMode: 'required',
  });
}
