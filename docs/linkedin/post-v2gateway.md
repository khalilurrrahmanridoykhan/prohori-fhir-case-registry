# LinkedIn post — Prohori (Phase L: HL7 v2 → FHIR)

**Image:** `prohori-v2gateway-linkedin.png`
**Alt text:** Dark-themed graphic titled "Most hospitals still speak HL7 v2,
not FHIR. I built the bridge." Below it, three side-by-side cards trace one
patient's ADT lifecycle: "ADT^A01 · ADMIT — Creates: Patient + Encounter,
If-None-Exist on MRN / visit №", "ADT^A08 · UPDATE — Updates: same Patient, in
place, PUT ?identifier=…", and "ADT^A03 · DISCHARGE — Finishes: same
Encounter, status → finished". A highlighted note below reads: "Before
claiming a live $transform, I checked: local HAPI advertises none for
StructureMap, and the engine the spec names couldn't be reached. So the FHIR
Mapping Language is authored and documented, not faked as running — the
honest answer beat the impressive-looking one." Footer lists: .NET 8 · NHapi
(ER7 parsing) · Firely SDK · FHIR Mapping Language · xUnit, with the GitHub
repo link.

**Live:** https://prohori-fhir-case-registry.vercel.app
**Source:** https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry

---

Most hospitals still don't speak FHIR. They speak HL7 v2 — pipe-delimited ADT/ORU/ORM messages, the same wire format since the 1990s, still carrying roughly 80% of real hospital traffic. If you want to work in health interoperability, you have to be fluent in the bridge, not just the destination.

So I built one: Prohori.V2Gateway.

→ Parses a raw ADT message with NHapi, the standard .NET HL7 v2 library — one code path handles admits, updates and discharges across HL7 v2 versions.

→ Maps it onto the exact same Patient/Encounter Bundle shape the rest of the registry already builds — the fourth way into that Bundle now, after the typed API, the SDC form, and the terminology-translated legacy import.

→ Gets idempotency right by message type, not a blanket rule: an admit (A01) creates; an update or discharge (A08/A03) conditionally *updates* the same record instead. Verified live — a changed address lands on the same patient; a discharge finishes the same encounter, it doesn't spawn a new one.

The part I'm most glad I did: before writing anywhere that this "transforms via FHIR Mapping Language," I actually checked whether that's true. My local FHIR server advertises no $transform operation for StructureMap. The dedicated engine the spec names wasn't reachable in my environment. So the mapping is authored as a real, reviewable FHIR Mapping Language file — and documented as declared, not executed. Not glossed over.

Built with .NET 8, NHapi, and the Firely SDK. All demonstration data is synthetic.

Live demo:
https://prohori-fhir-case-registry.vercel.app

Source code:
https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry

#FHIR #HL7 #HealthInteroperability #DigitalHealth #dotnet #HealthIT
