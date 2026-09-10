#!/usr/bin/env python3
"""Assert real backend auth, a full Group export, and a one-resource delta. Synthetic data only."""
import base64
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
TOKEN = 'http://localhost:8091/realms/prohori-bulk/protocol/openid-connect/token'
API = 'http://localhost:5280'
FHIR = 'http://localhost:8090/fhir'
CLIENT = 'prohori-bulk-client'


def request(url, data=None, headers=None, method=None):
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=data, headers=headers or {}, method=method), timeout=90) as response:
            return response.status, response.read(), response.headers
    except urllib.error.HTTPError as error:
        return error.code, error.read(), error.headers


def wait_ready(url):
    for attempt in range(180):
        if len(sys.argv) > 1:
            os.kill(int(sys.argv[1]), 0)  # fail if this script's API could not start (e.g. port collision)
        try:
            with urllib.request.urlopen(url, timeout=3) as response:
                if response.status == 200:
                    print('Ready:', url, flush=True)
                    return
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(2)
    raise RuntimeError('Readiness timeout: ' + url)


def b64(value):
    return base64.urlsafe_b64encode(value).rstrip(b'=').decode()


def assertion(**overrides):
    now = int(time.time())
    claims = dict(iss=CLIENT, sub=CLIENT, aud=TOKEN, iat=now, exp=now + 300, jti=uuid.uuid4().hex)
    claims.update(overrides)
    kid = json.loads((ROOT / '.bulk/jwks.json').read_text())['keys'][0]['kid']
    unsigned = b64(json.dumps(dict(alg='RS384', typ='JWT', kid=kid)).encode()) + '.' + b64(json.dumps(claims).encode())
    signature = subprocess.run(['openssl', 'dgst', '-sha384', '-sign', str(ROOT / '.bulk/client-key.pem')],
                               input=unsigned.encode(), capture_output=True, check=True).stdout
    return unsigned + '.' + b64(signature)


def grant(jwt, scope='system/*.read'):
    form = dict(grant_type='client_credentials', client_id=CLIENT,
        client_assertion_type='urn:ietf:params:oauth:client-assertion-type:jwt-bearer', client_assertion=jwt)
    if scope is not None:
        form['scope'] = scope
    data = urllib.parse.urlencode(form).encode()
    return request(TOKEN, data, {'Content-Type': 'application/x-www-form-urlencoded'})


for endpoint in [FHIR + '/metadata', TOKEN.replace('/protocol/openid-connect/token', '/.well-known/openid-configuration'), API + '/health']:
    wait_ready(endpoint)
discovery = json.loads(request(API + '/bulk/fhir/.well-known/smart-configuration')[1])
assert discovery['token_endpoint_auth_signing_alg_values_supported'] == ['RS384']
jwt = assertion()
status, body, _ = grant(jwt)
assert status == 200, f'Valid private_key_jwt failed: HTTP {status}'
token = json.loads(body)['access_token']
assert grant(jwt)[0] in (400, 401), 'Replayed assertion was accepted'
assert grant(assertion(aud='https://wrong.example/token'))[0] in (400, 401), 'Wrong audience accepted'
assert grant(assertion(exp=int(time.time()) - 60, iat=int(time.time()) - 360))[0] in (400, 401), 'Expired assertion accepted'
parts = assertion().split('.')
signature = bytearray(base64.urlsafe_b64decode(parts[2] + '=' * (-len(parts[2]) % 4)))
signature[0] ^= 1
assert grant('.'.join(parts[:2] + [b64(signature)]))[0] in (400, 401), 'Bad signature accepted'
assert request(API + '/bulk/fhir/$export')[0] == 401
status, body, _ = grant(assertion(), scope=None)
assert status == 200
assert request(API + '/bulk/fhir/$export', headers={'Authorization': 'Bearer ' + json.loads(body)['access_token']})[0] == 403
assert request(API + '/bulk/fhir/$export?_type=Binary', headers={'Authorization': 'Bearer ' + token, 'Prefer': 'respond-async'})[0] == 400
print('PASS: RS384 grant; replay, audience, expiry, signature, missing token and missing scope checks.', flush=True)

subprocess.run([sys.executable, 'scripts/seed-bulk.py'], cwd=ROOT, check=True)
output = Path(tempfile.mkdtemp(prefix='verify-', dir=ROOT / '.bulk'))
command = ['dotnet', 'src/Prohori.BulkClient/bin/Release/net8.0/Prohori.BulkClient.dll', '--output', str(output), '--timeout-seconds', '600']


def export(extra):
    before = set((output / 'runs').glob('*')) if (output / 'runs').exists() else set()
    subprocess.run(command + extra, cwd=ROOT, check=True, timeout=630)
    run, = set((output / 'runs').glob('*')) - before
    manifest = json.loads((run / 'manifest.json').read_text())
    assert {'transactionTime', 'request', 'requiresAccessToken', 'output', 'error'} <= manifest.keys()
    assert manifest['requiresAccessToken'] is True and manifest['error'] == []
    resources = []
    for index, entry in enumerate(manifest['output']):
        assert entry['url'].startswith(API + '/bulk/jobs/')
        assert request(entry['url'])[0] == 401, 'Unprotected download'
        for line in (run / f'{index:04d}.ndjson').read_text().splitlines():
            resource = json.loads(line)
            assert resource['resourceType'] == entry['type']
            resources.append(resource)
    return manifest, resources


full, resources = export([])
expected = {'Patient/bulk-dhaka', 'Patient/bulk-chattogram', 'Encounter/bulk-dhaka-visit', 'Encounter/bulk-chattogram-visit',
            'Observation/bulk-dhaka-rdt', 'Observation/bulk-chattogram-rdt', 'Condition/bulk-dhaka-condition'}
assert {r['resourceType'] + '/' + r['id'] for r in resources} == expected
assert len(resources) == 7
aggregate = json.loads((output / 'aggregate.json').read_text())
assert aggregate['patients'] == 2 and aggregate['rdtObservations'] == 2
rows = {row['division']: row for row in aggregate['divisions']}
assert rows['Dhaka']['positivityPercent'] == 100 and rows['Chattogram']['positivityPercent'] == 0
assert all(row['cases'] == 1 for row in rows.values())
print('PASS: full Group export = 7 resources; two divisions, one case each, 100% / 0% positivity.', flush=True)

# Change one observation after the manifest watermark, with optimistic concurrency.
url = FHIR + '/Observation/bulk-chattogram-rdt'
status, body, headers = request(url, headers={'Accept': 'application/fhir+json'})
assert status == 200
observation = json.loads(body)
observation['valueCodeableConcept']['coding'][0]['code'] = '10828004'
status, _, _ = request(url, json.dumps(observation).encode(),
    {'Content-Type': 'application/fhir+json', 'If-Match': headers['ETag']}, 'PUT')
assert status == 200
delta, resources = export(['--since', full['transactionTime']])
assert [(r['resourceType'], r['id']) for r in resources] == [('Observation', 'bulk-chattogram-rdt')], 'Delta leaked unchanged resources'
aggregate = json.loads((output / 'aggregate.json').read_text())
assert aggregate['snapshotResources'] == 7 and aggregate['downloadedResources'] == 1
assert all(row['positivityPercent'] == 100 and row['cases'] == 1 for row in aggregate['divisions'])
print('PASS: _since downloaded only the changed Observation; snapshot retained 7 resources and updated positivity.', flush=True)

# Keep only non-secret, synthetic evidence in the CI artifact directory.
evidence = ROOT / '.bulk/evidence'
evidence.mkdir(exist_ok=True)
for name, value in [('full-manifest.json', full), ('delta-manifest.json', delta), ('aggregate.json', aggregate)]:
    (evidence / name).write_text(json.dumps(value, indent=2) + '\n')
print('PASS: Bulk Data integration complete. Evidence: .bulk/evidence', flush=True)
