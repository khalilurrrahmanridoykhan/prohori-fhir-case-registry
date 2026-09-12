# LinkedIn post — Prohori (Phase J: Questionnaire & SDC)

**Image:** `prohori-sdc-linkedin.png`
**Alt text:** Dark-themed graphic titled "Two ways to submit a case. One Bundle
either way." Two side-by-side code panels compare a typed API call
(POST /cases) and a Structured Data Capture form submission
(POST /questionnaire-response/$extract), joined by an equals sign — both end
in the same `CaseBundleBuilder.Build()` call. A highlighted note below reads:
"Extraction maps answers to the same CaseSubmission the typed endpoint takes —
one Bundle-shape, two ways in, so the form inherits every conformance
guarantee the API already has." Footer lists: Questionnaire (C#) · $populate /
$extract · generic item-tree renderer · enableWhen, with the GitHub repo link.

**Live:** https://prohori-fhir-case-registry.vercel.app
**Source:** https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry

---

I built a form that produces a complete FHIR record — rendered from a real `Questionnaire` resource, not a hardcoded template.

A field case registry needs an intake form a community health worker can actually fill in. Building it the Structured Data Capture way means the form definition and the write API can never quietly drift apart.

What I implemented in Prohori:

→ A FHIR `Questionnaire` (patient demographics, disease, RDT result, visit date) rendered generically — the React form has no per-field logic; every label, choice and `enableWhen` rule (the diagnosis note only appears once the test is positive) comes straight off the resource.

→ `$populate`: look up a returning patient by National ID and prefill their demographics for a follow-up visit.

→ `$extract`: turn the filled-in `QuestionnaireResponse` into the exact same Patient/Encounter/Observation Bundle the typed API builds — one Bundle-shape, two ways in.

Built with .NET 8, the Firely SDK, FHIR R4 and React 19. 21 new backend tests plus mocked browser tests cover extraction, the `enableWhen` rule, and the `$populate` prefill.

The part I like most: because `$extract` hands off to the same Bundle-builder the typed API already uses, the form inherits every conformance guarantee for free — there's no second code path to keep correct.

The public dashboard is live. All demonstration data is synthetic.

Live demo:
https://prohori-fhir-case-registry.vercel.app

Source code:
https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry

#FHIR #SDC #HealthInteroperability #DigitalHealth #dotnet #ReactJS
