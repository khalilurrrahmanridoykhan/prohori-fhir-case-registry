# LinkedIn post — Prohori (Phase N: Publish a real Implementation Guide)

**Image:** `prohori-ig-publish-linkedin.png`
**Alt text:** Light, paper-toned graphic titled "I published a real FHIR
Implementation Guide," with a small decorative diagram in the top right of
four connected icon nodes (a document, a database, a pulse/observation
mark, and a checkmark) representing the IG's four resource profiles. Below
the title, a browser-window mockup with a green "LIVE" pulse badge shows
the real URL khalilurrrahmanridoykhan.github.io/prohori-fhir-case-registry
and the site's actual homepage — a layers icon, "Prohori Core — FHIR
profiles for the field case registry", and a nav bar (Home, Architecture,
Phases A–N, Artifacts Summary, Downloads). Three icon-labeled stat cards
read "22 (struck through) 5 — validator errors, all documented" beside a
shield-check icon, "4 — conformance profiles" beside a stacked-layers icon,
and "0 — broken links · bad canonicals" beside a broken-link icon. A
highlighted card titled "What the live terminology check caught" (warning
triangle icon) lists two corrected codes, each with its own icon: a
molecule icon beside "SNOMED CT · Malaria 84058000 (struck through) →
61462000", and a flask icon beside "LOINC · RDT result 70048-1 (struck
through) → 70569-9", with a note that neither code was real and both had
shipped silently since Phase C. A closing quote-marked line reads: "A
canonical URL doesn't have to resolve. A published IG's claims do — every
profile, every example, every code, checked by the same tool a government
IG would run." Footer lists the tech stack (FSH · SUSHI · HL7 IG Publisher
· Jekyll · GitHub Pages · GitHub Actions) beside a GitHub mark and the repo
link.

**Live IG:** https://khalilurrrahmanridoykhan.github.io/prohori-fhir-case-registry/en/index.html
**Live dashboard:** https://prohori-fhir-case-registry.vercel.app
**Source:** https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry

---

For fourteen phases, this FHIR registry's `ig/` folder only ever went as far as SUSHI — FSH compiled to JSON, and that's where it stopped. This phase, I ran the real thing: the HL7 IG Publisher, the same tool that builds every national and international Implementation Guide, including Bangladesh's own BD-Core.

→ Grew the profile set to four (added Encounter and Condition), authored a CapabilityStatement checked line-by-line against what the API actually implements, and wrote a full cross-referenced example case — all validated by the publisher itself, not a side script.

→ Along the way: caught a real security notice (the tutorial-standard `fhir.base.template` package name is known-insecure — switched to HL7's protected replacement), a removed SUSHI config key, and an undeclared Jekyll dependency that fails silently after several minutes of otherwise-clean build. None of that is dramatic. All of it is the difference between "runs on my machine once" and "actually ships."

→ The part that mattered: the IG Publisher validates every code in every ValueSet against a live terminology server, as a normal part of building. That caught two codes that were never real — a SNOMED code and a LOINC code for malaria, both wrong since Phase C, both silently accepted by every check before this one because nothing before Phase N ever validated a coded element's *value*, only its shape. Confirmed independently, fixed everywhere it appeared — the API, the dashboard, the bulk client, the seed script, every test.

Final state: 5 documented findings left in the build report, down from 22. Zero broken links, zero unresolvable canonicals, zero invalid codes. Live on GitHub Pages.

Built with FSH, SUSHI, the HL7 IG Publisher, and GitHub Actions. All demonstration data is synthetic.

Live IG:
https://khalilurrrahmanridoykhan.github.io/prohori-fhir-case-registry/en/index.html

Live dashboard:
https://prohori-fhir-case-registry.vercel.app

Source code:
https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry

#FHIR #HL7 #HealthInteroperability #DigitalHealth #ImplementationGuide #HealthIT
