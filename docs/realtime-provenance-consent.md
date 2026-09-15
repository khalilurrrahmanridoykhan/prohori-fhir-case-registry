# Real-time, Provenance & Consent (Phase O)

Three things every production FHIR deployment needs that a demo can skip:
push (so a new case shows up without a reload), audit (who created or read
what), and access control (a patient can say no). This phase adds a fourth
project — `Prohori.Subscriber` — plus two additions to every write
`Prohori.Api` already made.

## Subscriptions — a real rest-hook, not a poll loop

`Prohori.Subscriber` registers an R4 `Subscription` on the configured FHIR
server at startup (`criteria: Observation?_tag=urn:prohori|demo-cohort`,
`channel.type: rest-hook`), receives the callback HAPI delivers when a
matching Observation is created, and re-broadcasts a small summary over
Server-Sent Events to every connected dashboard tab. `useLiveUpdates`
(`web/src/realtime/useLiveUpdates.ts`) subscribes and invalidates the case
list on each event — a new visit appears within seconds, no reload.

```bash
docker compose -f deploy/docker-compose.yml up -d hapi
bash scripts/verify-realtime.sh
```

Verifies, against a real local HAPI: the Subscription gets created and HAPI
activates it (`requested` → `active`); a tagged Observation posted directly
to HAPI triggers a real rest-hook delivery; `Prohori.Subscriber` receives it
and broadcasts it (checked via `GET /debug/last-notification`, so the script
doesn't need to hold its own SSE connection open).

### Finding: subscriptions are off by default, and the config key isn't nested

HAPI's boot log said outright: *"Subscriptions are disabled on this server.
Subscriptions will not be activated."* The fix looks like it should be a
nested block —

```yaml
hapi:
  fhir:
    subscription:
      resthook:
        enabled: true   # WRONG — HAPI silently ignores this
```

— but HAPI's Spring Boot properties are flat, same style as the
`validation.requests_enabled` key already in `deploy/hapi/application.yaml`:

```yaml
    subscription:
      resthook_enabled: true   # what actually works
```

The nested form isn't rejected — it's just silently ignored, which is worse
than an error. Confirmed by reading the boot log for the line
`REST-hook subscriptions enabled` after the fix, not by trusting the docs.

### Finding: HAPI's rest-hook delivery is a PUT to `{endpoint}/{Type}/{id}`, not a POST to the literal URL

The natural assumption — "HAPI POSTs the resource to whatever URL I put in
`channel.endpoint`" — is wrong once `channel.payload` is set. HAPI's delivery
log showed:

```
PUT http://host.docker.internal:5300/notify?key=abc123/Observation/2
```

It treats `channel.endpoint` as a **FHIR base URL** and does a real `update()`
against `{endpoint}/{ResourceType}/{id}` — string-concatenated, so a query
string on the endpoint ends up *after* the appended path and breaks. Fixed by
moving the shared secret from a query parameter to a `channel.header`
(`X-Notify-Key: ...`, a real R4 `Subscription.channel.header` element) and
giving `/notify` two routes: a plain `POST /notify` for a server configured
to send an empty-payload ping, and `PUT /notify/{**rest}` — a catch-all —
for the payload-set delivery this project actually uses.

### Finding: conditional create doesn't update an existing match

The registration's `If-None-Exist: criteria=...` only *creates* when nothing
matches — it never updates an existing Subscription's `channel.header` if the
notify key changes between restarts. Caught by `scripts/verify-realtime.sh`
itself failing on its *second* run against the same long-lived local HAPI:
the first run's Subscription (with that run's key baked in) was still active
and matched the criteria, so the second run's freshly-generated key never
took effect and every delivery was rejected as unauthorized. Fixed by making
the script's key stable across runs rather than randomizing it — the honest
fix, since a real deployment's secret is expected to be stable too, not
rotated on every restart. If it does need to rotate, the old Subscription
has to be deleted (or updated directly) first; this project doesn't
implement that path.

## AuditEvent, not Provenance — and why

The plan named both. `AuditEvent` is the right fit here: it's about *who
accessed or changed a resource, when, from what system* — access-control
logging. `Provenance` is about *data lineage* — where a value's content
actually came from (a device, a derivation, a transform), which fits
`Prohori.V2Gateway`'s HL7 v2→FHIR mapping (Phase L) more than this project's
API writes. Every write through `FhirCaseService.SubmitAsync` — the same
single choke point Phase K/L/M/N all reused — now appends one `AuditEvent`
entry to the same transaction, naming every resource it wrote and the
authenticated caller's `sub` claim as the agent. Atomic with the case, not a
best-effort side call: if the transaction fails, no audit entry claiming a
write that didn't happen either.

## Consent — a default-permit record per patient, enforced in the API

Every new patient also gets a `Consent` (status `active`, scope
`patient-privacy`, provision `permit`) in the same transaction — conditional
create, so a returning patient's next visit doesn't spawn a second one.
`GET /patients/{nationalId}` checks it before returning
`Patient/{id}/$everything`; set to `deny`, it refuses with `403` and a real
`OperationOutcome` (`code: forbidden`), not a generic error shape.
`PUT /patients/{nationalId}/consent` toggles it.

```json
// GET /patients/19942691012345678 once denied
// -> 403
{
  "resourceType": "OperationOutcome",
  "issue": [{
    "severity": "error", "code": "forbidden",
    "diagnostics": "Patient 19942691012345678 has set Consent to deny. This read is refused."
  }]
}
```

**Break-glass**: `?breakGlass=true` with a `break-glass` scope reads anyway —
but the override is itself audited *before* the read happens (so it's on
record even if the read then fails), using the real v3 `BTG` ("break the
glass") purpose-of-use code, not a bespoke flag:

```json
"agent": [{ "who": {...}, "purposeOfUse": [{
  "coding": [{ "system": "http://terminology.hl7.org/CodeSystem/v3-ActReason", "code": "BTG" }]
}]}]
```

### Finding: the "right" search syntax breaks inside a transaction Bundle

The obvious way to dedupe the Consent — match it by the patient it belongs
to — is a chained or reference-modifier search:
`patient:identifier=<system>|<nid>` or `patient.identifier=<system>|<nid>`.
Both are valid FHIR, and both work as a **standalone** conditional create
(confirmed with a raw `curl -X POST .../Consent -H 'If-None-Exist: ...'`).
Inside a **transaction Bundle**, where the Consent also carries an unresolved
forward reference to the Patient entry's own `urn:uuid:` (resolved as part of
the same transaction), HAPI v8.0.0 breaks — first with
`HAPI-1250: Invalid/unsupported resource type: "identifier"` (the `:identifier`
modifier form), then with `HAPI-0389: Invalid match URL format` (the dot-chain
form, once more entries were added to the same bundle). Isolated by
binary-searching a captured outgoing bundle down to the smallest reproduction
with raw `curl` against local HAPI — removing entries one at a time until the
error changed or disappeared — rather than guessing from the error text
alone.

**Fixed by not chaining at all**: the Consent now carries its own
`identifier` (the same National ID, directly on the resource — a real R4
element, not a workaround field), and the conditional create matches on that:
`identifier=<system>|<nid>`. Same idempotency guarantee as the Patient's own
conditional create, no reference resolution involved, no transaction-specific
HAPI limitation to route around.

### The honest limit

`GET /patients/{nationalId}` is the enforcement point — but the **live
Vercel dashboard reads FHIR directly**, the same architecture every phase
since D has used for reads, and that path has no Consent check in front of
it. A real Consent interceptor belongs on the FHIR server itself (HAPI's own
Java interceptor chain, or a dedicated policy-enforcement proxy in front of
it) — out of scope for a .NET project fronting a server it doesn't operate.
The dashboard's `ConsentBadge` and its toggle only work with a write-scoped
SMART launch (local dev), same caveat Phase J's "New case" form already
carries.

## Dashboard

- **Live badge** (`Dashboard.tsx`) — connects to `Prohori.Subscriber`'s
  `/stream`; invisible, not an error state, when unreachable (the public
  deploy has no Subscriber running).
- **Audit trail** (`CaseDetail.tsx`) — `AuditEvent?entity=Patient/{id}`,
  sorted newest-first; a break-glass entry is visually distinct.
- **Consent badge + toggle** (`CaseDetail.tsx`) — reads `Consent` directly
  from FHIR (same as every other read in this dashboard); the toggle button
  only renders with a write-scoped SMART launch.

## What's not done

Real-world Consent is usually far richer than one permit/deny switch —
purpose-of-use restrictions, time-bounded provisions, per-actor exceptions.
This phase implements the two states the plan's "done when" gate actually
asks for (`GET` refused on deny, screen-recordable toggle) and documents the
rest as future scope rather than half-building it.
