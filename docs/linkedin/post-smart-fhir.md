I’ve built a SMART on FHIR workflow into Prohori, my FHIR-native field case registry.

A health app needs to know which patient is in context, what access has been granted, and whether a record has changed before an update is saved.

Here’s what I implemented:

→ Patient-aware launch: open the dashboard from a SMART-compatible environment and view the selected patient’s records.

→ Protected API writes: validate access tokens and enforce scopes using Keycloak, OAuth 2.0 and PKCE.

→ Safer updates: use FHIRPath/JSON Patch and version checks to prevent stale writes from silently overwriting newer changes.

Built with .NET 8, the Firely SDK, FHIR R4 and React.

I tested standalone and EHR launches against the SMART Health IT sandbox, plus the complete Keycloak/API flow locally. The checks include 43 .NET tests, browser tests, and real 401, 403 and 412 responses.

For me, this is the rewarding part of health interoperability engineering: making standards work together in running software.

The public dashboard is live. The repository includes the local security setup, documentation and demo recordings. All demonstration data is synthetic.

Live demo:
https://prohori-fhir-case-registry.vercel.app

Source code:
https://github.com/khalilurrrahmanridoykhan/prohori-fhir-case-registry

#FHIR #SMARTonFHIR #HealthInteroperability #DigitalHealth #DotNet

