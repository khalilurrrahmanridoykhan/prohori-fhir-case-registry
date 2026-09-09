using System.Security.Claims;

namespace Prohori.Api.Fhir;

/// <summary>A deliberately small, patient-scoped read facade for the local Keycloak demo.</summary>
public static class LocalSmartEndpoints
{
    public static void MapLocalSmart(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Smart:Enabled")) return;
        var authority = (app.Configuration["Auth:Authority"] ?? "http://localhost:8081/realms/prohori").TrimEnd('/');
        app.MapGet("/fhir/.well-known/smart-configuration", () => Results.Ok(new
        {
            authorization_endpoint = authority + "/protocol/openid-connect/auth",
            token_endpoint = authority + "/protocol/openid-connect/token",
            token_endpoint_auth_methods_supported = new[] { "none" },
            grant_types_supported = new[] { "authorization_code" },
            code_challenge_methods_supported = new[] { "S256" },
            scopes_supported = new[] { "openid", "fhirUser", "launch/patient", "patient/*.rs", "user/*.write" },
            // Keycloak context is resolved by /smart/context, not a conformant SMART token response.
            capabilities = new[] { "client-public", "sso-openid-connect" }
        }));
        app.MapGet("/smart/context", (ClaimsPrincipal user) =>
            Results.Ok(new { patient = user.FindFirstValue("patient"), fhirUser = user.FindFirstValue("fhirUser") }))
            .RequireAuthorization("PatientRead");
        app.MapGet("/fhir/Patient/{id}", async (string id, ClaimsPrincipal user, IHttpClientFactory clients, CancellationToken cancellation) =>
        {
            if (id != user.FindFirstValue("patient")) return Results.Forbid();
            return await Read(clients, $"Patient/{Uri.EscapeDataString(id)}", cancellation);
        }).RequireAuthorization("PatientRead");
        app.MapGet("/fhir/Encounter", async (ClaimsPrincipal user, IHttpClientFactory clients, CancellationToken cancellation) =>
            await Read(clients, $"Encounter?patient={Uri.EscapeDataString(user.FindFirstValue("patient")!)}&_include=Encounter:subject&_revinclude=Observation:encounter&_revinclude=Condition:encounter&_sort=-date&_count=300", cancellation))
            .RequireAuthorization("PatientRead");
        app.MapGet("/fhir/Patient/{id}/$everything", async (string id, ClaimsPrincipal user, IHttpClientFactory clients, CancellationToken cancellation) =>
        {
            if (id != user.FindFirstValue("patient")) return Results.Forbid();
            return await Read(clients, $"Patient/{Uri.EscapeDataString(id)}/$everything?_count=200", cancellation);
        }).RequireAuthorization("PatientRead");
    }

    private static async Task<IResult> Read(IHttpClientFactory clients, string path, CancellationToken cancellation)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.ParseAdd("application/fhir+json");
            using var result = await clients.CreateClient("fhir").SendAsync(request, cancellation);
            return Results.Text(await result.Content.ReadAsStringAsync(cancellation), "application/fhir+json", statusCode: (int)result.StatusCode);
        }
        catch (HttpRequestException) { return Results.Problem(statusCode: 502, detail: "FHIR server unavailable."); }
    }
}
