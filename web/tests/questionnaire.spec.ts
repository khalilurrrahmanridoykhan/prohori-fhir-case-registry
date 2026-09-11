import { expect, test } from '@playwright/test';

const QUESTIONNAIRE = {
  resourceType: 'Questionnaire',
  url: 'https://prohori.health/fhir/Questionnaire/prohori-case-questionnaire',
  status: 'draft',
  item: [
    {
      linkId: 'patient', text: 'Patient', type: 'group',
      item: [
        { linkId: 'patient.nationalId', text: 'Bangladesh National ID (NID)', type: 'string', required: true },
        { linkId: 'patient.familyName', text: 'Family name', type: 'string', required: true },
        { linkId: 'patient.givenNames', text: 'Given name(s)', type: 'string', repeats: true },
        {
          linkId: 'patient.gender', text: 'Gender', type: 'choice', required: true,
          answerOption: [{ valueCoding: { system: 'http://hl7.org/fhir/administrative-gender', code: 'male', display: 'Male' } }],
        },
        { linkId: 'patient.birthDate', text: 'Date of birth', type: 'date', required: true },
        { linkId: 'patient.city', text: 'City / upazila', type: 'string', required: true },
        { linkId: 'patient.district', text: 'District', type: 'string', required: true },
      ],
    },
    {
      linkId: 'disease', text: 'Suspected disease', type: 'choice', required: true,
      answerOption: [{ valueCoding: { system: 'http://snomed.info/sct', code: '38362002', display: 'Dengue fever' } }],
    },
    {
      linkId: 'rdtResult', text: 'Rapid diagnostic test result', type: 'choice', required: true,
      answerOption: [
        { valueCoding: { system: 'http://snomed.info/sct', code: '10828004', display: 'Positive' } },
        { valueCoding: { system: 'http://snomed.info/sct', code: '260385009', display: 'Negative' } },
      ],
    },
    { linkId: 'visitDate', text: 'Visit date/time', type: 'dateTime', required: true },
    {
      linkId: 'diagnosisNote', text: 'Diagnosis note', type: 'string',
      enableWhen: [{ question: 'rdtResult', operator: '=', answerCoding: { system: 'http://snomed.info/sct', code: '10828004' } }],
    },
  ],
};

/** Standalone-launches against a mocked EHR, landing signed-in on the dashboard — same
 * fixture smart.spec.ts uses — then hands back to the caller to exercise /new-case. */
async function launch(page: import('@playwright/test').Page) {
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
      return route.fulfill({ json: { access_token: 'synthetic-test-token', token_type: 'Bearer', patient: 'patient-one', scope: 'patient/*.rs user/*.write', expires_in: 300 } });
    }
    if (url.pathname === '/fhir/Patient/patient-one') return route.fulfill({ json: { resourceType: 'Patient', id: 'patient-one' } });
    if (url.pathname === '/fhir/Encounter') return route.fulfill({ json: { resourceType: 'Bundle', type: 'searchset', entry: [] } });
    throw new Error(`Unexpected EHR request: ${url.pathname}`);
  });
  await page.goto(`/launch.html?iss=${encodeURIComponent(issuer)}`);
  await expect(page.getByText('Patient patient-one', { exact: true })).toBeVisible();
}

test('New case without a SMART launch prompts for one instead of erroring', async ({ page }) => {
  await page.goto('/new-case');
  await expect(page.getByText(/write-scoped SMART launch/)).toBeVisible();
});

test('the form renders the live Questionnaire, hides diagnosisNote until positive, and extracts on submit', async ({ page }) => {
  await launch(page);

  let extractBody: unknown;
  await page.route('http://localhost:5279/questionnaire-response/**', async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/questionnaire')) return route.fulfill({ json: QUESTIONNAIRE }); // public, no bearer token
    expect(route.request().headers().authorization).toBe('Bearer synthetic-test-token');
    if (url.pathname.endsWith('$extract')) {
      extractBody = route.request().postDataJSON();
      return route.fulfill({ status: 201, json: { created: ['Patient/1', 'Encounter/2', 'Observation/3', 'Condition/4'] } });
    }
    throw new Error(`Unexpected API request: ${url.pathname}`);
  });

  await page.goto('/new-case');
  await expect(page.getByRole('heading', { name: 'New case' })).toBeVisible();
  await expect(page.getByLabel('Diagnosis note')).toHaveCount(0);

  await page.getByLabel('Bangladesh National ID (NID)').fill('19942691012345678');
  await page.getByLabel('Family name').fill('Khan');
  await page.getByLabel('Given name(s)').fill('Rahman');
  await page.getByLabel('Gender').selectOption('male');
  await page.getByLabel('Date of birth').fill('1995-06-15');
  await page.getByLabel('City / upazila').fill('Dhaka');
  await page.getByLabel('District').fill('Dhaka');
  await page.getByLabel('Suspected disease').selectOption('38362002');
  await page.getByLabel('Rapid diagnostic test result').selectOption('260385009');
  await expect(page.getByLabel('Diagnosis note')).toHaveCount(0); // still hidden: negative
  await page.getByLabel('Rapid diagnostic test result').selectOption('10828004');
  await expect(page.getByLabel('Diagnosis note')).toBeVisible(); // shown: positive
  await page.getByLabel('Visit date/time').fill('2026-08-14T09:20');

  await page.getByRole('button', { name: 'Submit case' }).click();

  await expect(page.getByText('Case submitted.')).toBeVisible();
  await expect(page.getByText('Encounter/2')).toBeVisible();
  const body = extractBody as { item: { linkId: string; item?: unknown[]; answer?: { valueCoding?: { code: string } }[] }[] };
  const disease = body.item.find((i) => i.linkId === 'disease');
  expect(disease?.answer?.[0]?.valueCoding?.code).toBe('38362002');
  const patientGroup = body.item.find((i) => i.linkId === 'patient');
  expect(patientGroup?.item).toHaveLength(7); // all seven demographic answers extracted
});

test('$populate prefills the patient group for a returning patient', async ({ page }) => {
  await launch(page);

  await page.route('http://localhost:5279/questionnaire-response/**', async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/questionnaire')) return route.fulfill({ json: QUESTIONNAIRE });
    if (url.pathname.endsWith('$populate')) {
      expect(route.request().postDataJSON()).toEqual({ nationalId: '19942691012345678' });
      return route.fulfill({
        json: {
          resourceType: 'QuestionnaireResponse', status: 'in-progress', questionnaire: QUESTIONNAIRE.url,
          item: [{
            linkId: 'patient',
            item: [
              { linkId: 'patient.nationalId', answer: [{ valueString: '19942691012345678' }] },
              { linkId: 'patient.familyName', answer: [{ valueString: 'Khan' }] },
              { linkId: 'patient.city', answer: [{ valueString: 'Dhaka' }] },
            ],
          }],
        },
      });
    }
    throw new Error(`Unexpected API request: ${url.pathname}`);
  });

  await page.goto('/new-case');
  await page.getByLabel('Returning patient? National ID').fill('19942691012345678');
  await page.getByRole('button', { name: 'Prefill' }).click();

  await expect(page.getByText(/Prefilled from an existing patient/)).toBeVisible();
  await expect(page.getByLabel('Family name')).toHaveValue('Khan');
  await expect(page.getByLabel('City / upazila')).toHaveValue('Dhaka');
});
