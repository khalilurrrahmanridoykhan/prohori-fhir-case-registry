import { expect, test } from '@playwright/test';

/** Standalone SMART launch landing on one patient's detail page — same fixture shape
 * smart.spec.ts / measure.spec.ts use, extended with the Phase O routes this page now
 * also queries (AuditEvent, Consent). */
async function launchOnPatientDetail(
  page: import('@playwright/test').Page,
  opts: { auditEntries?: unknown[]; consent?: { type: string } | null },
) {
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
      return route.fulfill({ json: { resourceType: 'Bundle', type: 'searchset', entry: [] } });
    }
    if (url.pathname === '/fhir/Patient/patient-one/$everything') {
      return route.fulfill({
        json: {
          resourceType: 'Bundle', type: 'searchset',
          entry: [{ resource: { resourceType: 'Patient', id: 'patient-one', identifier: [{ system: 'http://health.gov.bd/sid', value: '19942691012345678' }] } }],
        },
      });
    }
    if (url.pathname === '/fhir/AuditEvent') {
      const entries = opts.auditEntries ?? [];
      return route.fulfill({ json: { resourceType: 'Bundle', type: 'searchset', entry: entries.map((resource) => ({ resource })) } });
    }
    if (url.pathname === '/fhir/Consent') {
      const entry = opts.consent ? [{ resource: { resourceType: 'Consent', id: 'consent-1', status: 'active', provision: opts.consent } }] : [];
      return route.fulfill({ json: { resourceType: 'Bundle', type: 'searchset', entry } });
    }
    throw new Error(`Unexpected EHR request: ${url.pathname}`);
  });
  await page.goto(`/launch.html?iss=${encodeURIComponent(issuer)}`);
  await expect(page.getByText('Patient patient-one', { exact: true })).toBeVisible();
  await page.goto('/cases/patient-one');
}

test('an audit trail with entries renders the agent for each', async ({ page }) => {
  await launchOnPatientDetail(page, {
    auditEntries: [
      { resourceType: 'AuditEvent', id: 'audit-1', recorded: '2026-08-14T09:20:00Z', agent: [{ who: { display: 'test-agent' }, requestor: true }] },
    ],
    consent: null,
  });

  await expect(page.getByRole('heading', { name: 'Audit trail' })).toBeVisible();
  await expect(page.getByText('test-agent')).toBeVisible();
});

test('no audit events means no audit trail section — not an empty one', async ({ page }) => {
  await launchOnPatientDetail(page, { auditEntries: [], consent: null });

  await expect(page.getByRole('heading', { name: 'Audit trail' })).toHaveCount(0);
});

test('a permitted consent shows the permit badge', async ({ page }) => {
  await launchOnPatientDetail(page, { consent: { type: 'permit' } });

  await expect(page.getByText('Consent: permit', { exact: true })).toBeVisible();
});

test('a denied consent shows the deny badge', async ({ page }) => {
  await launchOnPatientDetail(page, { consent: { type: 'deny' } });

  await expect(page.getByText('Consent: deny', { exact: true })).toBeVisible();
});

test('an unreachable Subscriber leaves the dashboard working, with no live badge', async ({ page }) => {
  await page.route('http://localhost:5300/**', (route) => route.abort());
  await page.goto('/');

  await expect(page.getByRole('heading', { name: 'Case surveillance' })).toBeVisible();
  await expect(page.locator('.live-badge')).toHaveCount(0);
});
