# Demo video script (Phase P)

A 3-minute screen-recording storyboard. Written to be read once and
recorded in one or two takes — not a word-for-word script, a beat sheet.
Record locally (local HAPI + `Prohori.Api` + `Prohori.Subscriber` + the
dashboard dev server all running) so the live-update and write-scoped
features actually work — the public Vercel deploy is read-only.

## Setup before recording

```bash
docker compose -f deploy/docker-compose.yml up -d hapi
python3 scripts/seed-cohort.py http://localhost:8080/fhir
dotnet run --project src/Prohori.Api            # :5279
Fhir__BaseUrl=http://localhost:8080/fhir Subscriber__PublicUrl=http://host.docker.internal:5300 \
  ASPNETCORE_URLS=http://0.0.0.0:5300 dotnet run --project src/Prohori.Subscriber
cd web && npm run dev                            # :5173
```

Two browser tabs on the dashboard, side by side — one is how you'll show
the live-update badge actually doing something.

## Beat sheet (~3 minutes)

**0:00–0:20 — What this is**
"Prohori is a FHIR-native disease surveillance registry — a community
health worker records a field visit, it becomes a real FHIR transaction
Bundle, submitted to a real FHIR server. Fifteen build phases, each one a
merged PR against real servers — a public sandbox, a self-hosted HAPI
instance, and Bangladesh's actual national FHIR sandbox."

**0:20–0:50 — The dashboard, live**
Show the case list, filters, the positivity tile. Point out the small live
badge. In the second tab, submit a new case (New Case page, SMART launch
already done beforehand so this is just filling the form). Cut back to the
first tab: the case list updates within seconds, no reload — "that's a real
HAPI rest-hook Subscription delivering to a small service I wrote, pushed
over Server-Sent Events, not a poll loop."

**0:50–1:20 — One case, in depth**
Click into the new case's detail page. Show the timeline. Show the audit
trail entry — "every write gets an AuditEvent, atomic with the case." Show
the Consent badge, toggle it to deny, reload the page (or call
`GET /patients/{nationalId}` directly) to show the 403.

**1:20–1:50 — The Implementation Guide**
Switch to the live GitHub Pages IG site. Show the profile list, the
CapabilityStatement, the narrative pages. "This isn't SUSHI output —
it's built by the actual HL7 IG Publisher, the same tool a national IG
uses, and it validates every example and every terminology binding against
a live server."

**1:50–2:20 — One real finding**
Pick one: the BD-Core empty-ValueSet defect, or the malaria code bug the
IG's live terminology validation caught. "The IG Publisher validates every
code in every ValueSet against tx.fhir.org as part of a normal build — that
caught a SNOMED code and a LOINC code that had been wrong since an early
phase, silently, because nothing before that ever checked a coded element's
actual value." Show the before/after in `DECISIONS.md` or
`docs/publishing-the-ig.md` if there's time.

**2:20–2:50 — How it's built**
Quick cut to the repo: one branch, one PR, one tag per phase; CI running
12+ jobs against real Docker-backed servers, not mocks. "Twenty-six merged
PRs, a hundred and fifty .NET tests, a full Playwright suite — and every
phase's write-up documents not just what worked, but what I checked before
claiming it worked."

**2:50–3:00 — Close**
"Source, live dashboard, and the live IG are all linked below. Thanks for
watching."

## After recording

- Upload wherever it'll actually get watched (YouTube unlisted, LinkedIn
  native video, or attached to the CV/portfolio site).
- Link it from the README's top section and from
  `docs/architecture-case-study.md`.
- Keep the recording — a shorter cut (60–90s) works better as a LinkedIn
  post than the full walkthrough.
