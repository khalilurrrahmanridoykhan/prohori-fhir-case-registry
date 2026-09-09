#!/usr/bin/env python3
"""Create a local Keycloak realm from the PUBLIC part of a newly generated backend key."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
jwks = json.loads((root / '.bulk/jwks.json').read_text())
assert all('d' not in key for key in jwks['keys']), 'Only public keys belong in the realm'
realm = {
    'realm': 'prohori-bulk', 'enabled': True, 'sslRequired': 'external',
    'accessTokenLifespan': 300,
    'clientScopes': [{'name': 'system/*.read', 'protocol': 'openid-connect',
                      'attributes': {'include.in.token.scope': 'true'}}],
    'clients': [{
        'clientId': 'prohori-bulk-client', 'protocol': 'openid-connect',
        'publicClient': False, 'serviceAccountsEnabled': True,
        'standardFlowEnabled': False, 'directAccessGrantsEnabled': False,
        'clientAuthenticatorType': 'client-jwt', 'fullScopeAllowed': False,
        'defaultClientScopes': [], 'optionalClientScopes': ['system/*.read'],
        'attributes': {'use.jwks.url': 'false', 'jwks': json.dumps(jwks),
                       'token.endpoint.auth.signing.alg': 'RS384'},
        'protocolMappers': [{
            'name': 'api-audience', 'protocol': 'openid-connect',
            'protocolMapper': 'oidc-audience-mapper',
            'config': {'included.custom.audience': 'prohori-api',
                       'access.token.claim': 'true', 'id.token.claim': 'false'}
        }]
    }]
}
(root / '.bulk/prohori-bulk-realm.json').write_text(json.dumps(realm, indent=2) + '\n')
print('Registered prohori-bulk-client public JWKS for RS384 private_key_jwt.')
