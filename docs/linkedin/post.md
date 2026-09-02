# LinkedIn post — Bruno vs Postman for FHIR

**Image:** docs/linkedin/bruno-vs-postman-dark.png
**First comment:** https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry

---

Postman is the default. For health-data work, I think Bruno is the better call — and it's not close.

This week I started building Prohori, a FHIR-native field case registry, in the open. First step is the unglamorous one: probing a healthcare data server by hand before writing any application code. That collection of API calls isn't throwaway — it ships with the project. So the tool I keep it in matters.

Why Bruno wins here:

1. The collection lives in git, as plain text.
Every request is a .bru file in the repo. It shows up in pull requests. It diffs line by line. Your exact calls against a clinical server get reviewed next to the code — not locked in a vendor cloud, not dumped as a JSON blob that no one can read a diff of.

2. Offline-first. No account, no sync.
Postman pushes everything toward cloud workspaces. Anywhere near patient data — even a sandbox — you don't want request bodies, tokens and response history quietly syncing to a third party. Bruno runs fully local. Nothing leaves the machine.

3. Open source, so it's auditable.
When a hospital's security team asks "what does this tool send home?" the answer is "nothing — here's the source." Try answering that for a proprietary client.

4. The CLI is free.
bru run in CI, no paid seats, no run limits. Conformance-check your FHIR endpoints on every commit.

Postman is still the more feature-complete product — mock servers, monitors, a huge ecosystem. If your workflow lives there, stay. But for standards-based health integration, where the API collection is part of what you hand over, Bruno fits the grain of the work.

Collection's public — link in the comments.

#DigitalHealth #FHIR #HealthIT #HL7 #Interoperability #HealthTech
