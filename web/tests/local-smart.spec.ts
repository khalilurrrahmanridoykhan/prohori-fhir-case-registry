import { expect, test } from '@playwright/test';

test('local Keycloak launch, protected read and stale Patient write', async ({ page, request }) => {
  test.skip(process.env.PROHORI_LIVE_SMART !== '1', 'Requires seeded local HAPI, Keycloak and API.');
  await page.goto('/launch.html?iss=http%3A%2F%2Flocalhost%3A5279%2Ffhir');
  await page.getByLabel('Username or email').fill('demo');
  await page.getByLabel('Password', { exact: true }).fill('synthetic-demo-only');
  await page.getByRole('button', { name: 'Sign In', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Update Account Information' }).or(page.getByText('Patient prohori-smart-demo', { exact: true }))).toBeVisible({ timeout: 20000 });
  if (await page.getByRole('heading', { name: 'Update Account Information' }).isVisible()) {
    await page.locator('input[name="email"]').fill('demo@example.invalid');
    await page.locator('input[name="firstName"]').fill('Synthetic');
    await page.locator('input[name="lastName"]').fill('Demo');
    await page.getByRole('button', { name: 'Submit', exact: true }).click();
  }
  await expect(page.getByText('Patient prohori-smart-demo', { exact: true })).toBeVisible();
  await expect(page.getByText('Synthetic SMART Patient', { exact: true })).toBeVisible();
  const token = await page.evaluate(() => {
    for (const key of Object.keys(sessionStorage)) {
      try { const state = JSON.parse(sessionStorage.getItem(key)!); if (state.tokenResponse?.access_token) return state.tokenResponse.access_token as string; } catch { /* other session keys */ }
    }
    throw new Error('No access token');
  });
  const headers = { Authorization: `Bearer ${token}` };
  expect((await request.post('http://localhost:5279/cases', { data: {} })).status()).toBe(401);
  expect((await request.get('http://localhost:5279/fhir/Patient/another-patient/$everything', { headers })).status()).toBe(403);
  const patient = await request.get('http://localhost:8080/fhir/Patient/prohori-smart-demo');
  const resource = await patient.json();
  const patchHeaders = { ...headers, 'If-Match': patient.headers().etag, 'Content-Type': 'application/fhir+json', Prefer: 'return=representation' };
  const data = { resourceType: 'Parameters', parameter: [{ name: 'operation', part: [
    { name: 'type', valueCode: 'replace' }, { name: 'path', valueString: 'Patient.active' }, { name: 'value', valueBoolean: !resource.active },
  ] }] };
  const updated = await request.patch('http://localhost:5279/cases/prohori-smart-demo', { headers: patchHeaders, data });
  expect(updated.status(), await updated.text()).toBe(200);
  const stale = await request.patch('http://localhost:5279/cases/prohori-smart-demo', { headers: patchHeaders, data });
  expect(stale.status()).toBe(412);
  const jsonPatch = await request.patch('http://localhost:5279/cases/prohori-smart-demo', {
    headers: { ...headers, 'If-Match': updated.headers().etag, 'Content-Type': 'application/json-patch+json', Prefer: 'return=representation, respond-async' },
    data: [{ op: 'replace', path: '/active', value: resource.active }],
  });
  expect(jsonPatch.status(), await jsonPatch.text()).toBe(200);
  await page.screenshot({ path: '../docs/images/phase-h-keycloak.png', fullPage: true });
});
