using System.Security.Claims;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Prohori.Api.Models;

namespace Prohori.Api.Fhir;

/// <summary>
/// Reads gated by the patient's own <see cref="Consent"/>, and the endpoint that
/// toggles it. A real Consent interceptor belongs on the FHIR server itself (HAPI's
/// Java interceptor chain) — out of scope for a .NET project fronting it, so this
/// enforces it the same way <c>PatientWriteEndpoints</c>/<c>QuestionnaireEndpoints</c>
/// enforce everything else here: one gate in Prohori.Api, checked before every read
/// this API serves. See docs/realtime-provenance-consent.md for why, including the
/// honest limit: the live Vercel dashboard reads FHIR directly and bypasses this gate.
/// </summary>
public static class ConsentEndpoints
{
    public static void MapConsent(this WebApplication app)
    {
        app.MapGet("/patients/{nationalId}", async (string nationalId, IHttpClientFactory clients, ClaimsPrincipal user, bool breakGlass = false) =>
        {
            using var http = clients.CreateClient("fhir");

            var patientId = await FindPatientIdAsync(http, nationalId);
            if (patientId is null) return Results.NotFound();

            var decision = await FindConsentDecisionAsync(http, patientId);
            if (decision == ConsentDecision.Deny)
            {
                var hasBreakGlassScope = user.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Contains("break-glass", StringComparer.Ordinal);
                if (!breakGlass || !hasBreakGlassScope)
                    return Forbidden(nationalId);

                // Emergency override: read anyway, but the override itself is the one thing
                // that MUST be on record — recorded before the read, not after, so it exists
                // even if the read itself then fails.
                var audit = AuditEventBuilder.BuildBreakGlassRead(user.FindFirst("sub")?.Value, $"Patient/{patientId}", DateTimeOffset.UtcNow);
                using var auditContent = new StringContent(audit.ToJson(), System.Text.Encoding.UTF8, "application/fhir+json");
                await http.PostAsync("AuditEvent", auditContent);
            }

            using var everything = await http.GetAsync($"Patient/{patientId}/$everything?_count=200");
            var body = await everything.Content.ReadAsStringAsync();
            return Results.Text(body, "application/fhir+json", statusCode: (int)everything.StatusCode);
        })
        .RequireAuthorization("CaseRead")
        .WithSummary("Patient/{id}/$everything, gated by that patient's Consent — 403 + OperationOutcome if it's set to deny. " +
            "?breakGlass=true with the break-glass scope reads anyway, but is itself audited (v3 BTG purpose-of-use) before the read happens.");

        app.MapPut("/patients/{nationalId}/consent", async (string nationalId, ConsentToggleRequest body, IHttpClientFactory clients, ClaimsPrincipal user) =>
        {
            using var http = clients.CreateClient("fhir");

            // Consent's own identifier (not a chained patient:identifier= search) — see
            // ConsentBuilder's doc comment for why that carries its own identifier at all.
            var search = await http.GetAsync($"Consent?identifier={Systems.NationalId}|{Uri.EscapeDataString(nationalId)}&_count=1");
            if (!search.IsSuccessStatusCode) return Results.Problem(statusCode: 502, detail: "Could not search Consent on the FHIR server.");

            var bundle = FhirJsonDeserializer.DEFAULT.Deserialize<Bundle>(await search.Content.ReadAsStringAsync());
            var entry = bundle.Entry.FirstOrDefault(e => e.Resource is Consent);
            if (entry?.Resource is not Consent consent || entry.Resource.Id is null)
                return Results.NotFound(new { error = $"No Consent on file for national ID {nationalId}. One is created with the patient's first case." });

            consent.Provision = new Consent.provisionComponent
            {
                Type = body.Provision == ConsentDecision.Deny ? Consent.ConsentProvisionType.Deny : Consent.ConsentProvisionType.Permit,
            };

            using var content = new StringContent(consent.ToJson(), System.Text.Encoding.UTF8, "application/fhir+json");
            using var put = await http.PutAsync($"Consent/{consent.Id}", content);
            if (!put.IsSuccessStatusCode)
                return Results.Problem(statusCode: (int)put.StatusCode, detail: "The FHIR server rejected the Consent update.");

            return Results.Ok(new { nationalId, provision = body.Provision.ToString().ToLowerInvariant(), by = user.FindFirst("sub")?.Value });
        })
        .RequireAuthorization("CaseWrite")
        .WithSummary("Toggle a patient's Consent to permit or deny — deny then makes GET /patients/{nationalId} refuse with 403.");
    }

    private static async Task<string?> FindPatientIdAsync(HttpClient http, string nationalId)
    {
        using var response = await http.GetAsync($"Patient?identifier={Systems.NationalId}|{Uri.EscapeDataString(nationalId)}&_count=1");
        if (!response.IsSuccessStatusCode) return null;
        var bundle = FhirJsonDeserializer.DEFAULT.Deserialize<Bundle>(await response.Content.ReadAsStringAsync());
        return bundle.Entry.Select(e => e.Resource).OfType<Patient>().FirstOrDefault()?.Id;
    }

    private static async Task<ConsentDecision> FindConsentDecisionAsync(HttpClient http, string patientId)
    {
        using var response = await http.GetAsync($"Consent?patient={patientId}&status=active&_count=1");
        if (!response.IsSuccessStatusCode) return ConsentDecision.Permit; // no server-side interceptor either way — fail open, not closed, on a lookup error.
        var bundle = FhirJsonDeserializer.DEFAULT.Deserialize<Bundle>(await response.Content.ReadAsStringAsync());
        var consent = bundle.Entry.Select(e => e.Resource).OfType<Consent>().FirstOrDefault();
        return consent?.Provision?.Type == Consent.ConsentProvisionType.Deny ? ConsentDecision.Deny : ConsentDecision.Permit;
    }

    private static IResult Forbidden(string nationalId)
    {
        var outcome = new OperationOutcome();
        outcome.Issue.Add(new OperationOutcome.IssueComponent
        {
            Severity = OperationOutcome.IssueSeverity.Error,
            Code = OperationOutcome.IssueType.Forbidden,
            Diagnostics = $"Patient {nationalId} has set Consent to deny. This read is refused.",
        });
        return Results.Text(outcome.ToJson(), "application/fhir+json", statusCode: StatusCodes.Status403Forbidden);
    }
}
