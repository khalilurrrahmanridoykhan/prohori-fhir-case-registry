#!/usr/bin/env python3
"""
Seed a small, searchable demo cohort into a FHIR R4 server (Phase B).

Posts ONE transaction Bundle containing, per patient:
  Patient  -> Encounter (field visit) -> Observation (RDT result)
                                      -> Condition (only if the RDT is positive)

Every resource is tagged  urn:prohori | demo-cohort  so you can find and clean
up your own data on a shared server:  GET /Patient?_tag=urn:prohori|demo-cohort

Usage:
    python3 scripts/seed-cohort.py [BASE_URL]

BASE_URL default: https://hapi.fhir.org/baseR4
"""
import json
import sys
import time
import urllib.request

BASE = (sys.argv[1] if len(sys.argv) > 1 else "https://hapi.fhir.org/baseR4").rstrip("/")
RUN = str(int(time.time()))[-7:]           # unique-per-run suffix (dodges HAPI-2840)
TAG = {"system": "urn:prohori", "code": "demo-cohort", "display": "Prohori demo cohort"}

SID = "http://health.gov.bd/sid"
LOINC = "http://loinc.org"
SNOMED = "http://snomed.info/sct"
ICD10 = "http://hl7.org/fhir/sid/icd-10"
OBS_CAT = "http://terminology.hl7.org/CodeSystem/observation-category"
COND_CLIN = "http://terminology.hl7.org/CodeSystem/condition-clinical"
COND_VER = "http://terminology.hl7.org/CodeSystem/condition-ver-status"
ENC_CLASS = "http://terminology.hl7.org/CodeSystem/v3-ActCode"

TESTS = {
    "dengue":  {"code": "42239-4", "display": "Dengue virus NS1 Ag [Presence] in Serum or Plasma by Immunoassay"},
    "malaria": {"code": "70048-1", "display": "Plasmodium sp Ag [Presence] in Blood by Rapid immunoassay"},
}
DX = {
    "dengue":  {"sct": ("38362002", "Dengue fever"),   "icd": ("A90", "Dengue fever [classical dengue]")},
    "malaria": {"sct": ("84058000", "Malaria"),         "icd": ("B54", "Unspecified malaria")},
}

# name, gender, birthDate, city, district, disease, positive?, visitDate
COHORT = [
    ("Rahman Khan",       "male",   "1995-06-15", "Dhaka",       "Dhaka",       "dengue",  True,  "2026-08-04"),
    ("Fatima Begum",      "female", "1988-03-22", "Dhaka",       "Dhaka",       "dengue",  False, "2026-08-06"),
    ("Karim Uddin",       "male",   "2013-11-02", "Chattogram",  "Chattogram",  "malaria", True,  "2026-08-09"),
    ("Ayesha Siddiqua",   "female", "2001-01-30", "Sylhet",      "Sylhet",      "malaria", False, "2026-08-11"),
    ("Jamal Hossain",     "male",   "1974-09-10", "Narayanganj", "Narayanganj", "dengue",  True,  "2026-08-14"),
    ("Nasima Akter",      "female", "1996-07-19", "Dhaka",       "Dhaka",       "dengue",  True,  "2026-08-18"),
    ("Sohel Rana",        "male",   "2019-05-05", "Chattogram",  "Chattogram",  "malaria", False, "2026-08-21"),
    ("Mizanur Rahman",    "male",   "1959-12-01", "Sylhet",      "Sylhet",      "dengue",  False, "2026-08-25"),
]


def entry(fullurl, resource, url):
    return {"fullUrl": fullurl, "resource": resource,
            "request": {"method": "POST", "url": url}}


def build_bundle():
    entries = []
    for i, (name, gender, birth, city, district, disease, positive, visit) in enumerate(COHORT, 1):
        p, e, o = f"urn:uuid:patient-{i}", f"urn:uuid:encounter-{i}", f"urn:uuid:obs-{i}"
        family = name.split()[-1]
        given = name.split()[:-1]

        entries.append(entry(p, {
            "resourceType": "Patient",
            "meta": {"tag": [TAG]},
            "identifier": [{"system": SID, "value": f"{RUN}{i:04d}00000"}],
            "active": True,
            "name": [{"use": "official", "family": family, "given": given}],
            "gender": gender,
            "birthDate": birth,
            "address": [{"use": "home", "city": city, "district": district, "country": "Bangladesh"}],
        }, "Patient"))

        entries.append(entry(e, {
            "resourceType": "Encounter",
            "meta": {"tag": [TAG]},
            "status": "finished",
            "class": {"system": ENC_CLASS, "code": "FLD", "display": "field"},
            "subject": {"reference": p},
            "period": {"start": f"{visit}T09:00:00+06:00", "end": f"{visit}T09:30:00+06:00"},
        }, "Encounter"))

        t = TESTS[disease]
        entries.append(entry(o, {
            "resourceType": "Observation",
            "meta": {"tag": [TAG]},
            "status": "final",
            "category": [{"coding": [{"system": OBS_CAT, "code": "laboratory"}]}],
            "code": {"coding": [{"system": LOINC, "code": t["code"], "display": t["display"]}]},
            "subject": {"reference": p},
            "encounter": {"reference": e},
            "effectiveDateTime": f"{visit}T09:20:00+06:00",
            "valueCodeableConcept": {"coding": [{
                "system": SNOMED,
                "code": "10828004" if positive else "260385009",
                "display": "Positive" if positive else "Negative",
            }]},
        }, "Observation"))

        if positive:
            sct, icd = DX[disease]["sct"], DX[disease]["icd"]
            entries.append(entry(f"urn:uuid:cond-{i}", {
                "resourceType": "Condition",
                "meta": {"tag": [TAG]},
                "clinicalStatus": {"coding": [{"system": COND_CLIN, "code": "active"}]},
                "verificationStatus": {"coding": [{"system": COND_VER, "code": "confirmed"}]},
                "code": {"coding": [
                    {"system": SNOMED, "code": sct[0], "display": sct[1]},
                    {"system": ICD10, "code": icd[0], "display": icd[1]},
                ]},
                "subject": {"reference": p},
                "encounter": {"reference": e},
                "onsetDateTime": visit,
                "recordedDate": visit,
            }, "Condition"))

    return {"resourceType": "Bundle", "type": "transaction", "entry": entries}


def main():
    bundle = build_bundle()
    body = json.dumps(bundle).encode()
    req = urllib.request.Request(
        BASE, data=body, method="POST",
        headers={"Content-Type": "application/fhir+json", "Accept": "application/fhir+json"},
    )
    print(f"POST {BASE}  (transaction bundle, run id {RUN})")
    with urllib.request.urlopen(req) as resp:
        out = json.load(resp)

    created = {}
    for ent in out.get("entry", []):
        loc = ent.get("response", {}).get("location", "")
        rtype = loc.split("/")[0] if loc else "?"
        created[rtype] = created.get(rtype, 0) + 1
    print(f"  status {out.get('type')}  ->  " +
          ", ".join(f"{n} {t}" for t, n in sorted(created.items())))
    print()
    print("Find your cohort:")
    print(f"  {BASE}/Patient?_tag=urn:prohori|demo-cohort&_sort=-_lastUpdated")
    print(f"  run id this batch: {RUN}  (National IDs start with it)")


if __name__ == "__main__":
    main()
