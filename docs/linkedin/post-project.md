# LinkedIn post — Prohori (project reveal)

**Images (post in this order):**
1. `prohori-li-1-hero.png` — the thesis
2. `prohori-li-3-dashboard.png` — the live dashboard
3. `prohori-li-2-result.png` — validator + sandbox proof

**First comment:** https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry
**Live:** https://prohori-fhir-case-registry.vercel.app

---

Over the last few weeks I built a FHIR-native disease-surveillance registry end to end — and a case bundle it produces is now accepted by Bangladesh's national FHIR sandbox.

I work in FHIR interoperability, so I wanted a build that exercised the whole path, not just the easy read side. Seven phases, one pull request and one tag each, `main` demoable the whole way:

A — learn the REST API by hand (Bruno + curl)
B — search: every param type, _include / _revinclude, _has, $everything
C — a .NET 8 + Firely SDK write client: form → transaction Bundle, If-None-Exist conditional create, every OperationOutcome mapped to one RFC 7807 shape
D — a React 19 surveillance dashboard reading the FHIR server directly
E — my own HAPI FHIR server in Docker + a ProhoriPatient profile authored in FHIR Shorthand, enforced server-side
F — conform the model to BD-Core-FHIR-IG v0.4.6 and submit to the live DGHS government sandbox
G — ship it: dashboard on Vercel, reading the sandbox live

The BD-Core phase was the real test. UHID + NID sliced identifiers, Bangla and English name extensions, division/upazila geocodes, ICD-11 on the diagnosis. HL7's official validator: 0 errors. The DGHS sandbox: HTTP 200, resources created.

It also surfaced a genuine defect — BD-Core 0.4.6's bd-condition can't be submitted, because its required ICD-11 diagnosis ValueSet ships empty. Filed, and worked around on Encounter.reasonCode.

Synthetic data throughout. Everything is public — repo and live link in the comments.

If you're building FHIR systems, or hiring for them, I'd love to talk.

#FHIR #HL7 #HealthIT #Interoperability #DigitalHealth #dotnet #BDCore
