# Phase E — your own server + your own rules

Two things this phase adds:

1. a **local FHIR server** you control (HAPI + Postgres in Docker), so you stop
   depending on a shared sandbox that wipes;
2. a **conformance profile** (`ProhoriPatient`) that the registry — and the
   server — enforce on what gets written.

---

## 1. Run a local HAPI FHIR server

On macOS without Docker Desktop, use Colima for the daemon:

```bash
brew install colima docker docker-compose
colima start --cpu 4 --memory 6
```

Then:

```bash
docker compose -f deploy/docker-compose.yml up -d
# first boot builds the schema (~40s); wait for health, then:
curl -s http://localhost:8080/fhir/metadata | jq .software      # -> "8.0.0"
```

- `deploy/docker-compose.yml` — `hapiproject/hapi:v8.0.0`, **embedded H2**
  persisted to the `hapi-data` volume (`down -v` wipes it).
- `deploy/hapi/application.yaml` is layered on top of HAPI's bundled config via
  `SPRING_CONFIG_ADDITIONAL_LOCATION` — it only sets permissive CORS and
  `hapi.fhir.validation.requests_enabled: true`.
- **Why H2, not Postgres:** HAPI v8.0.0 bundles a Flyway (community) that rejects
  Postgres 15 *and* 16 (`Unsupported Database`), and disabling Flyway then trips a
  bean-init cycle. Not worth fighting for a local dev server — H2 is HAPI's
  default and works out of the box. The container runs as `root` so it can write
  the H2 file on the volume.

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
cd ig && sushi . --snapshot && cd ..
bash scripts/load-profile.sh                 # PUT the StructureDefinition into HAPI
```

Now a `POST /Patient` whose `meta.profile` names the profile is validated.
**Verified 2026-09-03** against the local server:

| POST body | Result |
| :--- | :--- |
| `patient-conformant.json` | `201 Created` |
| `patient-no-nid.json` | `422` — *"Patient.identifier: minimum required = 1, but only found 0"* + *"Slice 'nationalId': a matching slice is required"* |
| `patient-wrong-system.json` | `422` — *"Slice 'nationalId': a matching slice is required"* (the `mrn` identifier doesn't match the discriminator) |
| `patient-bad-nid.json` | `422` — *"Constraint failed: prohori-nid-digits: 'National ID is 10 to 17 digits.'"* |

The `Prohori.Api` (Phase C) runs against it with **no code change** —
`Fhir__BaseUrl=http://localhost:8080/fhir dotnet run --project src/Prohori.Api` —
and `POST /cases` created `Patient/6 · Encounter/3 · Observation/4 · Condition/5`.
(The API doesn't stamp `meta.profile` yet, so its writes aren't profile-checked —
that gets wired in in Phase F.)

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
