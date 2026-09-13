# CQL, Measure & MeasureReport (Phase M)

The dashboard's positivity tiles (Phase D) were client-side arithmetic —
`cases.filter(c => c.result === "positive").length / cases.length`, computed
in the browser from whatever the last search happened to return. Phase M
turns that into a **computable quality measure**: a declared `Measure` with
CQL-named populations, evaluated server-side over an explicit period, into a
real `MeasureReport`.

## The measure

One field visit (`Encounter`) is the unit of analysis. A visit is
numerator-positive if its linked `Observation`'s RDT result is SNOMED CT
`10828004` (Positive). There are no exclusions, so *Initial Population* and
*Denominator* are the same population — a straightforward `proportion`-scored
outcome measure, stratified by city.

| Population | CQL | What it counts |
| :--- | :--- | :--- |
| Initial Population | `exists("Field Visits In Period")` | Field visits with `Encounter.period.start` in the period |
| Denominator | `"Initial Population"` | Same — no exclusions |
| Numerator | `exists(... where a linked Observation is SNOMED 10828004)` | Of those, the RDT-positive ones |
| Stratifier | `Patient.address.city` | Same two counts, split by city |

`ig/input/cql/ProhoriDiseasePositivity.cql` is the full, real CQL — reviewable
on its own. `ig/input/fsh/ProhoriMeasure.fsh` declares the `Library` (the CQL
embedded as `content[0]`, `text/cql`) and the `Measure` (population
`criteria.expression`s referencing the CQL defines by name — standard
FHIR Clinical Reasoning shape).

## Declared in CQL, evaluated in C#

`MeasureEvaluator.cs` computes the same three populations by hand — not by
interpreting the CQL. Checked first, the same way Phase K checked `-tx n/a`
and Phase L checked `$transform`, before deciding:

- Local HAPI's clinical-reasoning module **does** load
  (`ca.uhn.fhir.cr.r4.measure.MeasureOperationsProvider` appears in the boot
  log once `hapi.fhir.cr.enabled: true` is set) — but calling
  `Measure/$evaluate-measure` still comes back *"does not know how to handle
  [this] operation"*. The provider needs a configured CQL execution
  `Repository` this Docker image doesn't wire up by default; getting a real
  CQL engine running was out of scope for the time available.
- The offline validator can't fully check the `Measure`↔`Library` link either:
  validating both resources together reports *"No compiled version of CQL
  found"* — it wants the CQL translated to ELM (a `cql-to-elm` compile step,
  a separate tool), not just the raw text `Library.content` carries. And,
  same finding as Phase K, checking `Library.content[0].contentType` against
  the real MIME-type registry needs `-tx` (network), which offline mode can't
  do — even for an unremarkable code like `text/cql`.

So `MeasureEvaluator` is the executable side of the declared CQL, the same
relationship every other phase's declared-but-not-live-executed artifact has
had to its C# implementation (Phase K's terminology bindings, Phase L's
StructureMap). The populations it computes are the ones the CQL names; if a
real CQL engine becomes available later, the `.cql` file is what you'd hand
it.

## Run it

```bash
docker compose -f deploy/docker-compose.yml up -d hapi
bash scripts/verify-measure.sh
```

Seeds the known 8-patient demo cohort (Phase B — 4 positive of 8, by city:
Dhaka 3/2, Chattogram 2/1, Sylhet 2/0, Narayanganj 1/1) and proves the
measure against it, live:

```
▸ full period (2026-08-01..2026-08-31) — expect denominator 8, numerator 4
  ✓ denominator=8 numerator=4
▸ narrow period (2026-08-01..2026-08-10) — expect denominator 3, numerator 2
  ✓ denominator=3 numerator=2
▸ empty period (a year with no visits) — expect denominator 0
  ✓ denominator=0
```

Ad hoc:

```bash
dotnet run --project src/Prohori.Api
curl "localhost:5279/measure/\$evaluate-measure?periodStart=2026-08-01&periodEnd=2026-08-31"
```

## The dashboard

`web/src/components/SummaryTiles.tsx` now tries the measure endpoint first —
`useMeasureReport()` fetches `GET /measure/$evaluate-measure` for the visible
date range and renders the numerator/denominator/positivity straight from the
`MeasureReport`, with a "computed" badge. If the API isn't reachable (the
live Vercel dashboard has no backend — same constraint as every phase since
C), it falls back to the original client-side count without an error state:
the tiles never break, they just quietly stop being measure-backed.
