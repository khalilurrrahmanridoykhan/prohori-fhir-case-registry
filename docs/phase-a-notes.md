# Phase A — API by hand: notes

Run against **`https://hapi.fhir.org/baseR4`** on 2026-09-02.
Server: HAPI FHIR `8.11.16-SNAPSHOT`, `fhirVersion` **4.0.1** (R4).

Reproduce everything: `bash scripts/phase-a.sh` (or open `bruno/` in Bruno and
run requests 01–08 top to bottom).

---

## 01 · `GET /metadata` — CapabilityStatement

What the server tells you about itself, before you send anything:

| Field | Value observed |
| :--- | :--- |
| `fhirVersion` | `4.0.1` |
| `software.name` / `version` | HAPI FHIR Server / `8.11.16-SNAPSHOT` |
| `implementation.url` | `https://hapi.fhir.org/baseR4` |
| `rest[0].mode` | `server` |
| `Patient` interactions | `create`, `read`, `vread`, `update`, `patch`, `delete`, `search-type`, `history-type`, `history-instance` |
| `Patient` search params (subset) | `_id`, `_tag`, `_lastUpdated`, `_profile`, `_has`, `_filter`, `identifier`, `name`, `family`, `given`, `birthdate`, `gender`, `address`, `address-city`, `address-country`, `organization`, `general-practitioner`, `active` |

**Takeaway:** always read this first for any server. It's the contract — which
resources, which interactions per resource, which search params. `_has` and
`_filter` being listed means reverse-chaining and the `_filter` param are
available (useful in Phase B).

---

## 02 · `POST /Patient` — create (server assigns id)

Request body: `fixtures/patient.json` (no `id`).

| Response | Value |
| :--- | :--- |
| Status | `201 Created` |
| `Location` | `https://hapi.fhir.org/baseR4/Patient/137870789/_history/1` |
| `ETag` | `W/"1"` |
| `Last-Modified` | `Wed, 02 Sep 2026 05:55:05 GMT` |
| body `id` | `137870789` (server-generated) |
| body `meta.versionId` | `1` |
| body `meta.lastUpdated` | `2026-09-02T01:55:05.397-04:00` |

**Takeaways:**
- You do **not** send `id` on create — the server owns it.
- The id you'll use everywhere else is in the `Location` header (and the body).
- `ETag: W/"1"` is the version — you feed it back as `If-Match` for safe updates.
- The `meta.tag` `urn:prohori|demo-cohort` survives the round-trip → this is how
  we isolate our own records on a shared public server.
- **Bug caught:** `HL7_FHIR_Leaning_plan.md` §3 has
  `"system": "[http://health.gov.bd/sid](http://health.gov.bd/sid)"` — Markdown
  link syntax leaked into JSON. `system` must be a bare URI, as in our fixture.

---

## 03 · `GET /Patient/{id}` — read

| Response | Value |
| :--- | :--- |
| Status | `200 OK` |
| `ETag` | `W/"1"` |
| body `meta.versionId` | `1` |
| `address[0].city` | `Dhaka` |

Returns the current version. `ETag` matches `meta.versionId`.

---

## 04 · `PUT /Patient/{id}` — update (client supplies id)

Body = whole resource with `id` set and `address[0].city` → `Narayanganj`.

| Response | Value |
| :--- | :--- |
| Status | `200 OK` |
| `ETag` | `W/"2"` |
| body `meta.versionId` | `2` |
| `address[0].city` | `Narayanganj` |

**Takeaways:**
- `id` in URL and body must match.
- PUT replaces the **whole** resource (no partial update — that's PATCH).
- `versionId` `1 → 2`; version 1 is still addressable at `/_history/1`.
- A PUT to an id that doesn't exist yet is an **upsert** on servers that allow
  client-assigned ids (HAPI public server does, with a numeric-id caveat).

---

## 05 · `GET /Patient/{id}/_history` — audit trail

| Response | Value |
| :--- | :--- |
| Status | `200 OK` |
| `Bundle.type` | `history` |
| `Bundle.total` | `2` |

| entry | `request.method` | `response.status` | `versionId` | city |
| :--- | :--- | :--- | :--- | :--- |
| 1 (newest) | `PUT` | `200 OK` | `2` | Narayanganj |
| 2 | `POST` | `201 Created` | `1` | Dhaka |

**Takeaway:** every version is retained, newest-first, and each entry records
*what operation* produced it. This is the built-in audit log — no extra work.

---

## 06 · `DELETE /Patient/{id}` — delete

| Response | Value |
| :--- | :--- |
| Status | `200 OK` |
| body | `OperationOutcome`, `severity: information`, `"Successfully deleted 1 resource(s). Took 6ms."` |

Soft delete — history is kept (request 05 still works after this).

---

## 07 · `GET /Patient/{id}` after delete — `410 Gone`

| Response | Value |
| :--- | :--- |
| Status | `410 Gone` |
| body | `OperationOutcome`, `severity: error`, `code: processing`, `"Resource was deleted at 2026-09-02T01:55:28.685-04:00"` |

**Takeaway:** `410 Gone` (existed, deleted) ≠ `404 Not Found` (never existed).
A client must distinguish them.

---

## 08 · `POST /Patient` with `"gender": "banana"` — `OperationOutcome`

| Response | Value |
| :--- | :--- |
| Status | `400 Bad Request` |
| body | `OperationOutcome` |
| `issue[0].severity` | `error` |
| `issue[0].code` | `processing` |
| `issue[0].diagnostics` | `HAPI-0450: Failed to parse request body as JSON resource. ... HAPI-1821: [element="gender"] Invalid attribute value "banana": Unknown AdministrativeGender code 'banana'` |

**Takeaways:**
- `gender` is bound to a **required** ValueSet (`male | female | other |
  unknown`) — an out-of-set value is rejected at parse time, before storage.
- `OperationOutcome` is the **single** error shape for every failed FHIR
  interaction. The Phase C client will parse `issue[].severity` / `code` /
  `diagnostics` / `expression` rather than any bespoke error body.
- HAPI reports this as a parse failure (`HAPI-0450`) because the code is invalid
  in the primitive itself; a cardinality/profile violation would instead come
  back as `code: invariant` / `required` with a FHIRPath in `expression`.

---

## Gotchas on the public HAPI server

- **`HAPI-2840` — duplicate-resource rejection.** Re-`POST`ing a byte-identical
  resource returns `412 Precondition Failed`
  (`"Can not create resource duplicating existing resource: Patient/…"`), even
  after the original was soft-deleted. `scripts/phase-a.sh` sidesteps this by
  giving each run a unique `identifier.value` + `given` name. Real servers
  usually don't do this; it's a public-sandbox guard against spam.
- **Shared + wiped.** Every resource here is world-visible and the server resets
  periodically. Never post real data. Always `meta.tag` your resources
  (`urn:prohori|demo-cohort`) so you can find and clean up your own.
- **Client-assigned ids.** `PUT /Patient/{new-id}` works as an upsert, but the
  public server prefers server-assigned numeric ids — use `POST` for creates.

## Concepts locked this phase

- **Service base URL** + how every endpoint hangs off it.
- **CapabilityStatement** as the server contract.
- **Logical `id`** (server/URL key) vs **business `identifier`** (National ID).
- **Versioning**: `meta.versionId` ↔ `ETag`; `_history`; vread.
- **Response headers**: `Location`, `ETag`, `Last-Modified`.
- **`OperationOutcome`** as the universal error/info payload.
- **`404` vs `410`**.
- **`meta.tag`** to scope your data on a shared server.
