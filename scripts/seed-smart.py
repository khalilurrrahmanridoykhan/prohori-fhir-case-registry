#!/usr/bin/env python3
"""Seed a fixed, synthetic patient for the local Keycloak demo (never the public sandbox)."""
import json
import urllib.request

base = 'http://localhost:8080/fhir'
resources = [
    {'resourceType': 'Patient', 'id': 'prohori-smart-demo', 'active': True,
     'name': [{'text': 'Synthetic SMART Patient'}], 'gender': 'male',
     'address': [{'city': 'Dhaka', 'country': 'Bangladesh'}]},
    {'resourceType': 'Encounter', 'id': 'prohori-smart-visit', 'status': 'finished',
     'class': {'system': 'http://terminology.hl7.org/CodeSystem/v3-ActCode', 'code': 'AMB'},
     'subject': {'reference': 'Patient/prohori-smart-demo'}, 'period': {'start': '2026-09-09T09:00:00+06:00'}},
    {'resourceType': 'Observation', 'id': 'prohori-smart-rdt', 'status': 'final',
     'code': {'coding': [{'system': 'http://loinc.org', 'code': '70895-3', 'display': 'Dengue virus NS1 Ag'}]},
     'subject': {'reference': 'Patient/prohori-smart-demo'}, 'encounter': {'reference': 'Encounter/prohori-smart-visit'},
     'effectiveDateTime': '2026-09-09T09:00:00+06:00',
     'valueCodeableConcept': {'coding': [{'system': 'http://snomed.info/sct', 'code': '10828004', 'display': 'Positive'}]}},
]
bundle = {'resourceType': 'Bundle', 'type': 'transaction', 'entry': [
    {'resource': resource, 'request': {'method': 'PUT', 'url': resource['resourceType'] + '/' + resource['id']}}
    for resource in resources]}
request = urllib.request.Request(base, data=json.dumps(bundle).encode(), headers={'Content-Type': 'application/fhir+json'})
with urllib.request.urlopen(request, timeout=90) as response:
    result = json.load(response)
    for entry in result['entry']:
        print(entry['response']['status'], entry['response'].get('location', ''))
