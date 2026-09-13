# Publishing the IG (Phase N)

Phases E/K/L/M each added a declarative FHIR artifact — a profile, terminology,
a mapping, a measure — but `ig/sushi-config.yaml` stayed `FSHOnly: true`: SUSHI
converted FSH to JSON and stopped there. Phase N turns that into what a FHIR
Solutions Architect actually ships: a **published Implementation Guide site**,
built by the real HL7 IG Publisher, live on GitHub Pages.

**Live IG:** https://khalilurrrahmanridoykhan.github.io/prohori-fhir-case-registry/

## What's new this phase

- Two more profiles — `ProhoriEncounter`, `ProhoriCondition` — bringing the
  total to four (`ProhoriPatient`, `ProhoriObservation` were Phases E/K).
  `ProhoriCondition.code` gets a new `required` binding to
  `ProhoriDiagnosisValueSet` (the two SNOMED codes `CaseBundleBuilder` writes).
- A full example set (`examples.fsh`): one coherent case — patient, encounter,
  observation, condition — each conformant to its profile and cross-referenced.
- `ProhoriCapabilityStatement` — what `Prohori.Api` actually implements,
  checked against `Program.cs`'s real endpoint mappings, referencing the real
  SDC/`$evaluate-measure`/`$translate` `OperationDefinition` canonicals.
- Narrative pages (`ig/input/pagecontent/*.md`): Home, Architecture (the
  system diagram from the README, expanded), and a full phase-by-phase build
  log with links back to every merged PR.

## Finding: the malaria SNOMED and LOINC codes were wrong — since Phase C

The IG Publisher validates every code in every ValueSet it hosts against a
live terminology server as part of a normal build. Doing that for
`ProhoriDiagnosisValueSet` and `ProhoriRdtTestValueSet` surfaced two errors
that had nothing to do with Phase N:

```
The code '84058000' is not valid in the system http://snomed.info/sct
The code '70048-1' is not valid in the system http://loinc.org
```

Both codes were wrong, not just unresolvable — confirmed independently
against `tx.fhir.org`'s own `$lookup` and `$expand`. `84058000` isn't a real
SNOMED CT concept at all; the correct code for "Malaria" is **`61462000`**.
`70048-1` isn't a real LOINC code either; the correct one for the RDT this
project models is **`70569-9`**, "Plasmodium sp Ag [Identifier] in Blood by
Rapid immunoassay" (LOINC's actual pairing for "Presence" is a *different*
method, `46094-9`/Immunoassay — the display text this project used,
"[Presence] ... by Rapid immunoassay", was never a real code+display
combination).

Dengue's codes (`38362002`, `42239-4`) were already independently verified —
accepted by DGHS's live sandbox in Phase F. Malaria's never were: every
malaria case built by `CaseBundleBuilder`, `BdCoreBundleBuilder`, the
Questionnaire's disease `answerOption`, and `seed-cohort.py`'s demo data has
carried these two wrong codes since **Phase C**, silently, because nothing
before Phase N's live terminology validation actually checked a coded
element's value against a real terminology server — `-tx n/a` (Phase K) and
the FHIR validator's own leniency both let it through unnoticed.

Fixed everywhere in this phase: `CaseBundleBuilder.cs`, `BdCoreBundleBuilder.cs`,
`QuestionnaireCatalog.cs`/`QuestionnaireExtraction.cs`,
`web/src/fhir/terminology.ts` (the dashboard's disease classifier),
`Prohori.BulkClient/Snapshot.cs`, `seed-cohort.py`, this IG's own ValueSets,
and every test that pinned the old values. **Not fixed**: any malaria case
already seeded onto the live DGHS sandbox by earlier runs of
`seed-cohort.py` still carries the old, wrong codes — re-seeding a live
government sandbox wasn't done as a side effect of this finding; the codes
new runs write are correct going forward.

## Running the real IG Publisher

```bash
mkdir -p ~/.fhir/ig-publisher
curl -fsSL -o ~/.fhir/ig-publisher/publisher.jar \
  "https://github.com/HL7/fhir-ig-publisher/releases/latest/download/publisher.jar"
cd ig && java -jar ~/.fhir/ig-publisher/publisher.jar -ig ig.ini
```

This is the actual HL7 tool (not a hand-rolled substitute) — it runs SUSHI
itself, resolves the template and every dependency, **validates every example
against its profile** (the plan's "examples must pass `$validate`"
requirement, satisfied by the publisher's own build step rather than a
separate script), and renders the full HTML site to `ig/output/`.

### Finding: `fhir.base.template` is a known-insecure package name

The obvious `template: fhir.base.template#current` (still what most SUSHI/IG
tutorials show) triggers, on first run:

```
This content depends on fhir.base.template which is no longer considered secure to use
See Security notification at https://www.fhir.org/guides/security-notices/2026-03-npm-dependencies.html
```

The notice: a security researcher registered a malicious package under that
exact name on the plain npm registry, which some FHIR tooling can resolve
dependencies against unsafely. HL7's own fix is `fhir2.base.template` — a
registered, protected replacement — and that's what `ig/ig.ini` actually
uses. Caught before the first successful build, not after.

### SUSHI's `template:` config key is gone

Newer SUSHI (3.20.1) no longer accepts `template:` in `sushi-config.yaml` —
it errors and tells you to manage `ig.ini` directly instead (a deliberate
SUSHI/IG-Publisher separation-of-concerns change). `ig/ig.ini` is that file:

```ini
[IG]
ig = fsh-generated/resources/ImplementationGuide-prohori.fhir.core.json
template = fhir2.base.template#current
```

### Finding: the base templates need Jekyll — a real, undeclared prerequisite

Everything up to the very last step — SUSHI, snapshot generation, narrative,
per-resource validation — runs on Java alone. The final HTML templating pass
for both `fhir.base.template` and its replacement `fhir2.base.template`
shells out to **Jekyll** (a Ruby static-site generator), and fails opaquely
if it isn't installed:

```
Publishing Content Failed: Cannot run program "jekyll" ... No such file or directory
```

Nothing in SUSHI's or the template's own docs surfaces this as a prerequisite
up front — it only appears once the build reaches that stage, after several
minutes of otherwise-successful work. Fixed locally with a modern Ruby
(macOS's bundled `/usr/bin/ruby` 2.6 is too old for current Jekyll) and
`gem install jekyll`; fixed in CI with `ruby/setup-ruby` before the publisher
step.

### Smaller fixes the publisher's validation caught

- **Resource id/canonical mismatches**: `ProhoriCapabilityStatement` and the
  Measure's `url` didn't end in the same segment as the Instance's own name
  (`RESOURCE_ID_MISMATCH`) — a real FHIR convention this project hadn't
  needed to satisfy before Phase N generated resources with hand-picked,
  independent `url`s and instance names.
- **`ConceptMap.sourceUri`/`targetUri` pointed at CodeSystems, not
  ValueSets** (`CONCEPTMAP_VS_NOT_A_VS`) — R4's actual modeling rule.
  `group.source`/`group.target` (the CodeSystems `$translate` matches codes
  against) were always correct; only the top-level `source[x]`/`target[x]`
  needed to become `sourceCanonical`/`targetCanonical` referencing two small
  ValueSets (one new: `ProhoriRdtResultLegacyValueSet`).
- **`CapabilityStatement.kind = instance` requires `implementation`** —
  added `implementation.description`/`.url`.
- **Two resources shared a title** ("Prohori RDT Result (legacy ODK codes)")
  — the CodeSystem and its now-required companion ValueSet; retitled.
- **`$extract`/`$populate` `operation.definition` canonicals didn't
  resolve** — they're real HL7 SDC IG operations, but `sushi-config.yaml`
  didn't declare a package dependency on `hl7.fhir.uv.sdc`. Added the
  dependency rather than drop the (required, once `operation[]` is used at
  all) `.definition` field.

### Why this job stays on the default live `tx.fhir.org`, unlike Phase K

`scripts/validate-ig.sh` (Phase K) runs the static validator with `-tx n/a` —
deliberately offline, because it only checks binding *enforcement* on a
couple of fixture files, and going online there once hung past 90 seconds.
The full IG Publisher build is a different question: it has to *expand* this
IG's own SNOMED-based ValueSets to publish them at all, and `-tx n/a` turns
that into a harder failure ("No server available") than an unchecked binding.
Tested both ways: offline, real; the live run completes in under two minutes
and is what actually caught the malaria code error above — worth the network
dependency here.

### What's left in `qa.html`, and why it's left

After every fix above, the build reports **5 errors** — down from an initial
22 — all expected:

- Four are `MEASURE_M_CRITERIA_CQL_NO_ELM`: Phase M's own finding, that no
  live CQL engine compiles `ProhoriDiseasePositivity.cql` to ELM in this
  phase (see `docs/measures.md`). Tried suppressing them via the
  `path-suppressed-warnings` IG parameter (it exists, confirmed in HL7's own
  `ig-parameters` CodeSystem) — the publisher rejected the file as "not using
  the new format" without documenting what that format actually is. Chasing
  an underdocumented cosmetic fix wasn't worth it; the finding stays visible
  and explained rather than hidden behind a mechanism that didn't work.
- One is the publisher's own "Supressed messages file not found" notice —
  emitted regardless of whether a suppression file is configured; harmless.

Zero broken links, zero invalid XHTML, zero resource id/url mismatches, zero
unresolvable canonicals, zero invalid terminology codes.

## CI: build on every push, deploy on `main`

The `ig-publisher` job runs the full SUSHI + IG Publisher build on every push
and PR (so a broken IG — an invalid example, a bad profile — fails CI the
same way a broken test does); it only deploys to GitHub Pages when the push
lands on `main`. The publisher jar and its FHIR package cache are both
cached (`actions/cache`) — a cold run downloads ~250MB and several package
dependencies; a warm one doesn't.
