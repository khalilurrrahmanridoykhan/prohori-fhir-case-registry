# Phase E — your own server + your own rules

Two things this phase adds:

1. a **local FHIR server** you control (HAPI + Postgres in Docker), so you stop
   depending on a shared sandbox that wipes;
2. a **conformance profile** (`ProhoriPatient`) that the registry — and the
   server — enforce on what gets written.

---

## 1. Run a local HAPI FHIR server

```bash
docker compose -f deploy/docker-compose.yml up -d
# wait for health, then:
curl -s http://localhost:8080/fhir/metadata | jq .software
```

- `deploy/docker-compose.yml` — `hapiproject/hapi:v8.0.0` + `postgres:16`.
- `deploy/hapi/application.yaml` — R4, JSON, permissive CORS, and
  `hapi.fhir.validation.requests_enabled: true` (validate every write).
- Data persists in the `hapi-pgdata` volume. `docker compose … down -v` wipes it.

Point the rest of the stack at it — **no code change**, just config:

```bash
Fhir__BaseUrl=http://localhost:8080/fhir dotnet run --project src/Prohori.Api
# web/.env:  VITE_FHIR_BASE=http://localhost:8080/fhir
```

---

## 2. The ProhoriPatient profile

`ig/input/fsh/ProhoriPatient.fsh` — FHIR Shorthand. Tightens base `Patient`:

| Rule | Why |
| :--- | :--- |
| `identifier` sliced by `system`; slice `nationalId` `1..1` fixed to `http://health.gov.bd/sid` | every patient must carry a Bangladesh National ID |
| invariant `prohori-nid-digits` — `value` matches `^[0-9]{10,17}$` | catch malformed NIDs |
| `name 1..*`, `name.family 1..1` | a name is required |
| `gender 1..1`, `birthDate 1..1` | required demographics |

Build it (needs Node; no Java, no IG Publisher). `ig/fsh-generated/` is
gitignored — regenerate it before validating or loading the profile:

```bash
npm install -g fsh-sushi
cd ig && sushi . --snapshot
# -> ig/fsh-generated/resources/StructureDefinition-prohori-patient.json  (with snapshot)
#    ig/fsh-generated/resources/Patient-prohori-patient-example.json
```

---

## 3. Enforce it — two layers

### Server-side (HAPI)

```bash
docker compose -f deploy/docker-compose.yml up -d
bash scripts/load-profile.sh                 # PUT the StructureDefinition into HAPI
```

Now a `POST /Patient` whose `meta.profile` names the profile is validated;
a non-conformant one comes back `400` / `422` with an `OperationOutcome`.

### CI-side (the official validator)

```bash
bash scripts/validate-ig.sh
```

Downloads `validator_cli.jar` (HL7's reference validator) and checks the profile
against expectation fixtures in `ig/input/tests/`:

| Fixture | Expected |
| :--- | :--- |
| `patient-conformant.json` | passes |
| `patient-no-nid.json` | fails — no identifier |
| `patient-wrong-system.json` | fails — identifier system isn't the NID system |
| `patient-bad-nid.json` | fails — `value` isn't 10–17 digits |
| `patient-no-birthdate.json` | fails — `birthDate` missing |

CI job `IG — build profile + validate` runs SUSHI then this script on every push.

---

## Why the official validator, not the Firely SDK validator

The plan called for an `Hl7.Fhir.Specification` xUnit test. In Firely SDK 6.x the
validation story is split: the legacy in-process `Validator` is gone, and
`Firely.Fhir.Validation` 3.x gates snapshot generation behind an Enterprise
licence, so a simple "validate this instance against this profile" test isn't
straightforward on the free tier. `validator_cli.jar` is HL7's reference
implementation, is exactly what Phase F needs for the BD-Core package anyway, and
runs fine on Java 17+. Decision logged in `DECISIONS.md`.

---

## Concepts locked this phase

- **StructureDefinition** as a *constraint* on a base resource; differential vs
  snapshot.
- **Slicing** — `discriminator` (type `value`, path `system`), named slices,
  cardinality on a slice.
- **Fixed values** and **FHIRPath invariants**.
- **FHIR Shorthand + SUSHI** as the authoring toolchain.
- **`meta.profile`** as the claim a resource makes, and server-side request
  validation as the enforcement.
