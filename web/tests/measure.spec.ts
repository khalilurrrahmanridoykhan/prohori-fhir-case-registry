import { expect, test } from '@playwright/test';

/** Standalone SMART launch against a mocked EHR, landing on the dashboard scoped to one
 * patient with two visits — same fixture shape smart.spec.ts uses. */
async function launchWithTwoVisits(page: import('@playwright/test').Page) {
  const issuer = 'https://ehr.example/fhir';
  await page.route('https://ehr.example/**', async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/.well-known/smart-configuration')) {
      return route.fulfill({ json: { authorization_endpoint: 'https://ehr.example/auth', token_endpoint: 'https://ehr.example/token', code_challenge_methods_supported: ['S256'] } });
    }
    if (url.pathname === '/auth') {
      return route.fulfill({ status: 302, headers: { Location: `${url.searchParams.get('redirect_uri')}?code=demo&state=${url.searchParams.get('state')}` } });
    }
    if (url.pathname === '/token') {
      return route.fulfill({ json: { access_token: 'synthetic-test-token', token_type: 'Bearer', patient: 'patient-one', scope: 'patient/*.rs', expires_in: 300 } });
    }
    if (url.pathname === '/fhir/Patient/patient-one') return route.fulfill({ json: { resourceType: 'Patient', id: 'patient-one', address: [{ city: 'Dhaka' }] } });
    if (url.pathname === '/fhir/Encounter') {
      return route.fulfill({
        json: {
          resourceType: 'Bundle', type: 'searchset',
          entry: [
            { resource: { resourceType: 'Encounter', id: 'enc-1', subject: { reference: 'Patient/patient-one' }, period: { start: '2026-08-04T09:00:00+06:00' } } },
            { resource: { resourceType: 'Encounter', id: 'enc-2', subject: { reference: 'Patient/patient-one' }, period: { start: '2026-08-06T09:00:00+06:00' } } },
            { resource: { resourceType: 'Patient', id: 'patient-one', address: [{ city: 'Dhaka' }] } },
            { resource: { resourceType: 'Observation', id: 'obs-1', encounter: { reference: 'Encounter/enc-1' }, code: { coding: [{ system: 'http://loinc.org', code: '42239-4' }] }, valueCodeableConcept: { coding: [{ system: 'http://snomed.info/sct', code: '10828004' }] } } },
            { resource: { resourceType: 'Observation', id: 'obs-2', encounter: { reference: 'Encounter/enc-2' }, code: { coding: [{ system: 'http://loinc.org', code: '42239-4' }] }, valueCodeableConcept: { coding: [{ system: 'http://snomed.info/sct', code: '260385009' }] } } },
          ],
        },
      });
    }
    throw new Error(`Unexpected EHR request: ${url.pathname}`);
  });
  await page.goto(`/launch.html?iss=${encodeURIComponent(issuer)}`);
  await expect(page.getByText('Patient patient-one', { exact: true })).toBeVisible();
}

test('a successful $evaluate-measure adds a measure tile with the reported counts', async ({ page }) => {
  await page.route('http://localhost:5279/measure/**', (route) => route.fulfill({
    json: {
      resourceType: 'MeasureReport', status: 'complete', type: 'summary',
      group: [{ population: [
        { code: { coding: [{ code: 'initial-population' }] }, count: 2 },
        { code: { coding: [{ code: 'denominator' }] }, count: 2 },
        { code: { coding: [{ code: 'numerator' }] }, count: 1 },
      ] }],
    },
  }));
  await launchWithTwoVisits(page);

  const tile = page.locator('.tile--measure');
  await expect(tile).toBeVisible();
  await expect(tile.getByText('1/2', { exact: true })).toBeVisible();
});

test('an unreachable measure endpoint leaves the rest of the dashboard working, with no measure tile', async ({ page }) => {
  await page.route('http://localhost:5279/measure/**', (route) => route.abort());
  await launchWithTwoVisits(page);

  await expect(page.locator('.tile--measure')).toHaveCount(0);
  await expect(page.getByText('Positivity', { exact: true })).toBeVisible(); // the client-computed tiles are unaffected
});
