import { expect, test } from '@playwright/test';

for (const ehr of [false, true]) {
  test(`${ehr ? 'EHR' : 'standalone'} launch exchanges a PKCE code and scopes reads`, async ({ page }) => {
    const issuer = 'https://ehr.example/fhir';
    let challenge = '';
    await page.route('https://ehr.example/**', async route => {
      const url = new URL(route.request().url());
      if (url.pathname.endsWith('/.well-known/smart-configuration')) {
        return route.fulfill({ json: { authorization_endpoint: 'https://ehr.example/auth', token_endpoint: 'https://ehr.example/token', code_challenge_methods_supported: ['S256'] } });
      }
      if (url.pathname === '/auth') {
        challenge = url.searchParams.get('code_challenge')!;
        expect(challenge.length).toBeGreaterThan(40);
        expect(url.searchParams.get('code_challenge_method')).toBe('S256');
        expect(url.searchParams.get('aud')).toBe(issuer);
        expect(url.searchParams.get('launch')).toBe(ehr ? 'ehr-context' : null);
        return route.fulfill({ status: 302, headers: { Location: `${url.searchParams.get('redirect_uri')}?code=demo&state=${url.searchParams.get('state')}` } });
      }
      if (url.pathname === '/token') {
        const body = new URLSearchParams(route.request().postData()!);
        expect(body.get('code_verifier')!.length).toBeGreaterThan(40);
        const { createHash } = await import('node:crypto');
        expect(createHash('sha256').update(body.get('code_verifier')!).digest('base64url')).toBe(challenge);
        return route.fulfill({ json: { access_token: 'synthetic-test-token', token_type: 'Bearer', patient: 'patient-one', scope: 'patient/*.rs', expires_in: 300 } });
      }
      if (url.pathname === '/fhir/Patient/patient-one') return route.fulfill({ json: { resourceType: 'Patient', id: 'patient-one' } });
      if (url.pathname === '/fhir/Encounter') {
        expect(url.searchParams.get('patient')).toBe('patient-one');
        expect(url.searchParams.has('_tag')).toBe(false);
        expect(route.request().headers().authorization).toBe('Bearer synthetic-test-token');
        return route.fulfill({ json: { resourceType: 'Bundle', type: 'searchset', entry: [] } });
      }
      throw new Error(`Unexpected request: ${url.pathname}`);
    });
    await page.goto(`/launch.html?iss=${encodeURIComponent(issuer)}${ehr ? '&launch=ehr-context' : ''}`);
    await expect(page.getByText('Patient patient-one', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Case surveillance' })).toBeVisible();
    await page.goto('/cases/patient-two');
    await expect(page.getByText(/Patient is outside the launch context/)).toBeVisible();
  });
}

test('an unknown OAuth state fails closed', async ({ page }) => {
  await page.goto('/?code=untrusted&state=unknown');
  await expect(page.getByText(/SMART launch failed/)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Case surveillance' })).toHaveCount(0);
});
