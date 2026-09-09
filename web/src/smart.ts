import FHIR from 'fhirclient';
import type Client from 'fhirclient/lib/Client';

export let smartClient: Client | undefined;
export async function initializeSmart() {
  const params = new URLSearchParams(location.search);
  if (params.has('code') || params.has('state') || sessionStorage.getItem('prohori-smart')) {
    smartClient = await FHIR.oauth2.ready();
    if (!smartClient.patient.id) throw new Error('Launch did not provide patient context. Relaunch with launch/patient.');
    sessionStorage.setItem('prohori-smart', '1');
    history.replaceState(null, '', location.pathname);
  }
}
export function launchSmart() {
  return FHIR.oauth2.authorize({
    clientId: import.meta.env.VITE_SMART_CLIENT_ID ?? 'prohori-dashboard',
    scope: 'launch launch/patient openid fhirUser patient/*.rs',
    redirectUri: new URL('/', location.href).href,
    iss: new URLSearchParams(location.search).get('iss') ?? import.meta.env.VITE_SMART_ISS ?? 'https://launch.smarthealthit.org/v/r4/fhir',
    pkceMode: 'required',
  });
}
