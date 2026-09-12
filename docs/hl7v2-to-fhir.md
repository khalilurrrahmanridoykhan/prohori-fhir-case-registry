# HL7 v2 → FHIR (Phase L)

Roughly 80% of real hospital traffic is still HL7 v2 — ADT (patient movement),
ORU (results), ORM (orders) — not FHIR. `Prohori.V2Gateway` is a small facade:
it accepts a raw ER7 ADT message, parses it with the same parser a real
integration engine would use, maps it to FHIR, and submits the transaction
Bundle to the configured server. It's the "translate at the boundary, reuse
what already works behind it" pattern this project has now used three times —
Phase J's Questionnaire `$extract` and Phase K's legacy-code `$translate` both
end at the same `CaseBundleBuilder`; this one builds its own Bundle the same
way, for a source format that was never going to be FHIR-shaped to begin with.

## Run it

```bash
docker compose -f deploy/docker-compose.yml up -d hapi   # or point at any FHIR server
bash scripts/verify-v2-gateway.sh
```

Verifies, against a real server: an `A01` admit creates a fresh Patient +
Encounter; an `A08` for the same MRN updates the *same* Patient (a changed
address actually lands); an `A03` discharge finishes the *same* Encounter
(`status: finished`, `period.end` set) rather than creating a new one.

```bash
dotnet run --project src/Prohori.V2Gateway   # -> http://localhost:5279 by default
curl -X POST localhost:5279/adt -H 'Content-Type: text/plain' --data-binary @message.hl7
# ?dryRun=true returns the Bundle without submitting
```

## Segment → resource map

| HL7 v2 | Field | FHIR | Note |
| :--- | :--- | :--- | :--- |
| `MSH-9-2` | Trigger event | — | Drives idempotency (below), nothing else |
| `PID-3-1` | MRN | `Patient.identifier` (`.../v2-mrn`) | The conditional-create/update key |
| `PID-5-1`/`-2` | Family/given name | `Patient.name.family`/`.given` | One given name captured (v2 repeats; simplified) |
| `PID-7-1` | Birth date (`YYYYMMDD`) | `Patient.birthDate` | |
| `PID-8-1` | Sex (Table 0001) | `Patient.gender` | M/F/O → male/female/other; U/A/N → unknown |
| `PID-11-3` | City | `Patient.address.city` | |
| `PID-11-4` | State/Province | `Patient.address.district` | A simplification — v2's State/Province isn't really Bangladesh's district administrative level, but it's the closest XAD component for a synthetic demo |
| `PV1-2-1` | Patient class (Table 0004) | `Encounter.class` (v3-ActCode) | I→IMP, O/R/B→AMB, E→EMER, P→PRENC, unmapped→AMB (never fails the message) |
| `PV1-19-1` | Visit Number | `Encounter.identifier` (`.../v2-visit-number`) | The conditional-create/update key. Absent → no Encounter entry at all |
| `PV1-44-1` | Admit date/time | `Encounter.period.start` | Assumed Bangladesh local time (no TZ in v2 TS) |
| `PV1-45-1` | Discharge date/time | `Encounter.period.end`, and `status = finished` | |

Where this differs from HL7's own [v2-to-fhir](https://build.fhir.org/ig/HL7/v2-to-fhir/)
reference maps: that project is the real, comprehensive HL7v2→FHIR crosswalk —
every PID/PV1 field, every v2 table, every edge case, maintained by HL7 itself.
This is a small slice of it (the fields one ADT-driven case-registry admit
actually needs), hand-picked and hand-mapped, not generated from the reference
maps. A production integration engine should start from v2-to-fhir's actual
StructureMaps, not this one.

## Idempotency: why A01 differs from A08/A03

A retried `A01` must not create a second Patient; an `A08` correcting a
demographic typo, or an `A03` discharge, must actually land on the *existing*
record, not silently no-op. So the trigger event picks the FHIR interaction,
not just a field value:

| Trigger | Patient | Encounter |
| :--- | :--- | :--- |
| `A01` (admit) | `POST` + `If-None-Exist` on MRN | `POST` + `If-None-Exist` on Visit Number |
| `A08` (update), `A03` (discharge) | `PUT Patient?identifier=...` (conditional update) | `PUT Encounter?identifier=...` (conditional update) |

Both reference each other inside the transaction via `urn:uuid:` fullUrls —
the same cross-reference idiom `CaseBundleBuilder` uses — which the server
resolves to the real (created-or-matched) ids regardless of which HTTP verb
each entry used.

## The StructureMap: authored, not executed

`ig/input/maps/hl7v2-to-fhir.map` is genuine FHIR Mapping Language — the
PID→Patient / PV1→Encounter declaration the plan calls for. It is **not**
converted to a StructureMap resource or run through a live `$transform` in
this phase:

- Local HAPI v8.0.0's own `CapabilityStatement` advertises no `transform`
  operation for `StructureMap` (only `create`/`read`/`update`/`patch`/
  `validate`/`meta*` — checked directly against the running server).
- **matchbox** — the dedicated FHIR Mapping Language engine the plan names as
  the alternative — publishes to a registry this environment couldn't reach
  within scope; standing up an unfamiliar Java service on faith, without being
  able to verify it works, was the wrong trade for the time available.

So, the same relationship `CaseBundleBuilder` has always had to any formal
mapping declaration (there wasn't one, before this phase): the `.map` file is
the standards-based, human-reviewable statement of intent; `V2ToFhirMapper.cs`
is the executable implementation, hand-kept in sync with it (the segment table
above is the sync check). If a real `$transform` engine becomes available
later, the `.map` is what you'd hand it.

## Why `/adt` isn't authenticated

Every other write path in this project (`/cases`, `$extract`,
`/legacy-import/cases`) requires a `CaseWrite`-scoped bearer token (Phase H).
A real HL7 v2 ADT feed is different: it arrives over MLLP from a hospital's
internal interface engine, on a private network segment, not from a browser or
a public API caller — bearer-token auth doesn't model that trust boundary, and
bolting on Phase H's SMART/OAuth2 machinery here would duplicate it without
teaching anything new. `/adt` is unauthenticated by design; a production
deployment would sit it behind network-level access control (VPN, firewall
rules, an MLLP listener that only accepts connections from known hosts) —
not a bearer token.

## Not implemented

**MLLP** (Minimal Lower Layer Protocol — the actual transport real HL7 v2
interfaces use, TCP with `0x0B`/`0x1C 0x0D` framing) — `/adt` takes plain HTTP
POST instead, which is far easier to test and demo, at the cost of not being
what a real interface engine speaks. An MLLP listener would sit in front of
the same `V2Parser`/`V2ToFhirMapper` pair unchanged.
