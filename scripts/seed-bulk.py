#!/usr/bin/env python3
"""Seed two synthetic field visits and an explicit patient Group in the loopback Bulk Data lab."""
import json
import urllib.request
from pathlib import Path

base = 'http://localhost:8090/fhir'
resources = []
for suffix, division, result in [('dhaka', 'Dhaka', '10828004'), ('chattogram', 'Chattogram', '260385009')]:
    patient = 'bulk-' + suffix
    resources.extend([
        {'resourceType': 'Patient', 'id': patient, 'active': True,
         'name': [{'text': 'Synthetic ' + division + ' Patient'}], 'gender': 'unknown',
         'address': [{'state': division, 'country': 'Bangladesh'}]},
        {'resourceType': 'Encounter', 'id': patient + '-visit', 'status': 'finished',
         'class': {'system': 'http://terminology.hl7.org/CodeSystem/v3-ActCode', 'code': 'AMB'},
         'subject': {'reference': 'Patient/' + patient}, 'period': {'start': '2026-09-09T09:00:00+06:00'}},
        {'resourceType': 'Observation', 'id': patient + '-rdt', 'status': 'final',
         'code': {'coding': [{'system': 'http://loinc.org', 'code': '42239-4'}]},
         'subject': {'reference': 'Patient/' + patient}, 'encounter': {'reference': 'Encounter/' + patient + '-visit'},
         'effectiveDateTime': '2026-09-09T09:00:00+06:00',
         'valueCodeableConcept': {'coding': [{'system': 'http://snomed.info/sct', 'code': result}]}},
    ])
resources.append({'resourceType': 'Condition', 'id': 'bulk-dhaka-condition',
    'clinicalStatus': {'coding': [{'system': 'http://terminology.hl7.org/CodeSystem/condition-clinical', 'code': 'active'}]},
    'code': {'coding': [{'system': 'http://snomed.info/sct', 'code': '38362002'}]},
    'subject': {'reference': 'Patient/bulk-dhaka'}, 'encounter': {'reference': 'Encounter/bulk-dhaka-visit'}})
resources.append({'resourceType': 'Group', 'id': 'prohori-cohort', 'type': 'person', 'actual': True,
                  'name': 'Prohori synthetic bulk cohort', 'quantity': 2,
                  'member': [{'entity': {'reference': 'Patient/bulk-' + suffix}} for suffix in ['dhaka', 'chattogram']]})
for resource in resources:
    resource['meta'] = {'tag': [{'system': 'urn:prohori', 'code': 'bulk-demo'}]}
bundle = {'resourceType': 'Bundle', 'type': 'transaction', 'entry': [
    {'fullUrl': base + '/' + r['resourceType'] + '/' + r['id'], 'resource': r,
     'request': {'method': 'PUT', 'url': r['resourceType'] + '/' + r['id']}} for r in resources]}
request = urllib.request.Request(base, data=json.dumps(bundle).encode(), headers={'Content-Type': 'application/fhir+json'})
try:
    with urllib.request.urlopen(request, timeout=90) as response:
        for entry in json.load(response)['entry']: print(entry['response']['status'], entry['response'].get('location'))
except urllib.error.HTTPError as error:
    print(error.read().decode())
    raise
