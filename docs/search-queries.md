# Phase B — FHIR search query catalogue

Run against **`https://hapi.fhir.org/baseR4`** on 2026-09-02 (HAPI FHIR 8.11.x, R4).

Reproduce: `python3 scripts/seed-cohort.py` then `bash scripts/phase-b.sh`.
Every query is scoped with `_tag=urn:prohori|demo-cohort` so it returns only the
seeded cohort, not the shared server's other data.

> URL note: `|` and `$` are shown unencoded for readability. In a real client
> pass them through URL-encoding (`%7C`, `%24`) or let the client do it — Bruno
> and `curl -G --data-urlencode` handle it.

---

## The cohort

8 patients, each with a field `Encounter`, an `Observation` (RDT result), and —
if the result is positive — a `Condition`.

| # | Name | Sex | Born | City | Disease | RDT | Dx |
|---|------|-----|------|------|---------|-----|-----|
| 1 | Rahman Khan | M | 1995-06-15 | Dhaka | dengue | **positive** | dengue |
| 2 | Fatima Begum | F | 1988-03-22 | Dhaka | dengue | negative | — |
| 3 | Karim Uddin | M | 2013-11-02 | Chattogram | malaria | **positive** | malaria |
| 4 | Ayesha Siddiqua | F | 2001-01-30 | Sylhet | malaria | negative | — |
| 5 | Jamal Hossain | M | 1974-09-10 | Narayanganj | dengue | **positive** | dengue |
| 6 | Nasima Akter | F | 1996-07-19 | Dhaka | dengue | **positive** | dengue |
| 7 | Sohel Rana | M | 2019-05-05 | Chattogram | malaria | negative | — |
| 8 | Mizanur Rahman | M | 1959-12-01 | Sylhet | dengue | negative | — |

Totals: `Patient` 8 · `Encounter` 8 · `Observation` 8 (5 dengue NS1, 3 malaria RDT;
4 positive) · `Condition` 4.

---

## 1 · String parameters

String search is **starts-with, case- and accent-insensitive** by default, matched
against every relevant sub-part of the element.

| # | Intent | Query | Result |
|---|--------|-------|--------|
| 1 | Name begins with "na" | `Patient?name=na` | **1** — Nasima Akter |
| 2 | Exact match on one name part | `Patient?name:exact=Khan` | **1** — matches the `family` part exactly |
| 3 | Exact match on family | `Patient?family:exact=Khan` | **1** |
| 4 | Substring anywhere | `Patient?name:contains=hos` | **1** — Jamal **Hos**sain |
| 5 | City | `Patient?address-city=Dhaka` | **3** — Rahman, Fatima, Nasima |

**Gotcha:** `name:exact=Rahman Khan` returns **0**. `name` is matched against each
*individual* name part (`given`, `family`, `prefix`, `text`…), and `:exact`
demands an exact match on one of them. "Rahman Khan" is not stored as a single
string — `given` is `["Rahman"]`, `family` is `"Khan"`. Use `family:exact` +
`given:exact`, or search `name=rahman&name=khan` (starts-with, ANDed).

---

## 2 · Token parameters

Tokens match coded / identifier / boolean values as `system|code`, `|code`
(no system), or bare `code`.

| # | Intent | Query | Result |
|---|--------|-------|--------|
| 6 | Patient by National ID | `Patient?identifier=http://health.gov.bd/sid\|<value>` | **1** — exact system+value match |
| 7 | Female patients | `Patient?gender=female` | **3** |
| 8 | Not male (`:not`) | `Patient?gender:not=male` | **3** — excludes `male`; would also keep `other`/`unknown` |
| 9 | Dengue NS1 tests | `Observation?code=http://loinc.org\|42239-4` | **5** |
| 10 | Positive results (any disease) | `Observation?value-concept=http://snomed.info/sct\|10828004` | **4** |
| 11 | Confirmed dengue diagnoses | `Condition?code=http://snomed.info/sct\|38362002` | **3** |

---

## 3 · Composite parameters

Composite params bind two components with `$` so they must match **the same
occurrence** of a repeating element.

| # | Intent | Query | Result |
|---|--------|-------|--------|
| 12 | *Positive* **dengue NS1** specifically | `Observation?code-value-concept=http://loinc.org\|42239-4$http://snomed.info/sct\|10828004` | **3** — Rahman, Jamal, Nasima |

Contrast: `code=...42239-4&value-concept=...10828004` as two separate params would
also match a patient with a *negative* NS1 **and** a positive malaria RDT. The
composite prevents that cross-match.

---

## 4 · Date parameters & prefixes

Prefixes: `eq` (default) `ne` `gt` `lt` `ge` `le` `sa` (starts-after) `eb` (ends-before) `ap` (approximately).

| # | Intent | Query | Result |
|---|--------|-------|--------|
| 13 | Born on a date | `Patient?birthdate=1995-06-15` | **1** |
| 14 | Born in the 1990s | `Patient?birthdate=ge1990-01-01&birthdate=le1999-12-31` | **2** — Rahman (1995), Nasima (1996) |
| 15 | Children (born after 2010) | `Patient?birthdate=gt2010-01-01` | **2** — Karim (2013), Sohel (2019) |
| 16 | Visits from 15 Aug 2026 on | `Observation?date=ge2026-08-15` | **3** — Nasima, Sohel, Mizanur |

Repeating the same date param ANDs the two bounds — the idiomatic way to express a
range.

---

## 5 · Modifiers

| # | Intent | Query | Result |
|---|--------|-------|--------|
| 17 | Alive (no deceased date) | `Patient?death-date:missing=true` | **8** |
| 18 | No GP on file | `Patient?general-practitioner:missing=true` | **8** |

`:missing=true` → element absent; `:missing=false` → element present. Other useful
modifiers: `:not`, `:exact`, `:contains`, `:in` / `:not-in` (value against a
ValueSet), `:above` / `:below` (subsumption), `:identifier` (match a reference by
its business id).

**Gotcha:** `Patient?_has:Condition:subject:code:missing=false` returned no
`total` and 0 entries — HAPI does not honour a `:missing` modifier *inside* a
`_has` chain. Modifiers on reverse-chained params are patchy across servers;
verify against your target.

---

## 6 · `_include` / `_revinclude`

Pull referenced resources into the same `Bundle` in **one round trip**.

| # | Intent | Query | Result |
|---|--------|-------|--------|
| 19 | Each NS1 observation **+ its patient** | `Observation?code=http://loinc.org\|42239-4&_include=Observation:subject` | 5 Observation + 5 Patient = **10 entries** (`total` still 5 — matches only) |
| 20 | Each patient **+ all their observations** | `Patient?_tag=…&_revinclude=Observation:subject` | 8 Patient + 8 Observation = **16 entries** |

`_include` walks a reference *forward* (Observation → its subject).
`_revinclude` walks *backward* (Patient ← Observations that point at it).
Included resources have `search.mode = include`, not `match`.

---

## 7 · Chaining & reverse chaining (`_has`)

| # | Intent | Query | Result |
|---|--------|-------|--------|
| 21 | Observations whose patient is named "Khan" | `Observation?subject.name=Khan` | **1** |
| 22 | Patients **who have** a positive result | `Patient?_has:Observation:subject:value-concept=http://snomed.info/sct\|10828004` | **4** |
| 23 | Patients **who have** a dengue Condition | `Patient?_has:Condition:subject:code=http://snomed.info/sct\|38362002` | **3** |

- **Chained** (`subject.name`): filter resource A by a field on the resource it
  references (B). Constrain the type with `subject:Patient.name` if the reference
  is polymorphic.
- **`_has`** (reverse chain): filter resource A by the existence of resource B
  that references A. Reads as `_has:<type>:<ref-param-back-to-A>:<param-on-B>`.
  This is how you ask "patients with …" without the patient carrying that data.

---

## 8 · Result control

| # | Intent | Query | Result |
|---|--------|-------|--------|
| 24 | Just the count | `Patient?_tag=…&_summary=count` | `total=8`, **0 entries** |
| 25 | Thin projection | `Patient?_tag=…&_elements=name,birthDate` | 8 patients, each trimmed to `name` + `birthDate` (+ mandatory elements), `meta.tag SUBSETTED` |
| 26 | Sort + limit | `Condition?_tag=…&_sort=-recorded-date&_count=2` | 2 of 4, newest first, `Bundle.link[relation=next]` present |
| 27 | Page size | `Patient?_tag=…&_count=3` | 3 of 8, `next` link present |

Other `_summary` values: `true` (top-level + mandatory), `data` (drop `text`),
`text` (only narrative + mandatory), `false`.

---

## 9 · Pagination

`_count` sets the page size; the server returns a `Bundle` with
`link[relation=self|next|previous|first|last]`. **Follow `next` verbatim** — it's
an opaque URL with the server's cursor/offset token. Don't build page 2 by hand.

```
GET /Patient?_tag=urn:prohori|demo-cohort&_count=3
  -> 3 entries + link rel=next -> .../?_getpages=<uuid>&_getpagesoffset=3&_count=3
GET <that next link>
  -> next 3 entries + a new next link
... until no next link.
```

---

## 10 · The one that matters — a whole case in a single call

```
GET /Patient/{id}/$everything
```

Returns a `Bundle` of the patient and every resource in their compartment —
here **4 resources**: `Patient` + `Encounter` + `Observation` + `Condition`.

This is the query the Phase D dashboard uses to render a case timeline. Where
`$everything` isn't available, the fallback is
`Patient?_id={id}&_revinclude=Encounter:subject&_revinclude=Observation:subject&_revinclude=Condition:subject`.

---

## Concepts locked this phase

- The 5 param types: **string** (starts-with), **token** (`system|code`),
  **date** (with prefixes), **reference**, **number/quantity** — plus **composite**.
- Repeating a param **ANDs** it; comma-separating values **ORs** them.
- **Modifiers** change match semantics; support varies by server and by param.
- **`_include` / `_revinclude`** fetch related resources in one request;
  `search.mode` distinguishes `match` from `include`.
- **Chaining** filters by a referenced resource; **`_has`** filters by a
  referencing resource.
- **`_summary` / `_elements`** shrink the payload; **`_sort` / `_count`** order
  and page; always **follow the `next` link**.
- **`$everything`** assembles a full patient record server-side.
