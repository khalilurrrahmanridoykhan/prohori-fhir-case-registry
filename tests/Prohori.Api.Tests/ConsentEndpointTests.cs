using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Prohori.Api.Tests;

/// <summary>
/// In-process tests of the consent-gated read and its toggle, with a fake "fhir"
/// HttpClient standing in for the FHIR server — same pattern as
/// <c>LegacyImportEndpointTests</c>. The real end-to-end behavior (a Subscription
/// notification, a live Consent write) is proven against local HAPI by
/// scripts/verify-realtime.sh.
/// </summary>
public class ConsentEndpointTests(AuthenticatedFactory factory) : IClassFixture<AuthenticatedFactory>
{
    private const string NationalId = "19942691012345678";

    private WebApplicationFactory<Program> Host(HttpMessageHandler handler) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => services.AddHttpClient("fhir").ConfigurePrimaryHttpMessageHandler(() => handler)));

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }

    private static HttpResponseMessage FhirJson(int status, string body) =>
        new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/fhir+json") };

    private static string PatientBundle(string id) =>
        "{\"resourceType\":\"Bundle\",\"type\":\"searchset\",\"entry\":[{\"resource\":{\"resourceType\":\"Patient\",\"id\":\"" + id + "\"}}]}";

    // Mirrors what ConsentBuilder.Build actually produces (base R4 Consent requires
    // scope and category, both 1..1/1..* — a fixture that skips them fails Firely's
    // strict deserializer the same way an under-specified real server response would.
    private static string ConsentBundle(string id, string provision) =>
        "{\"resourceType\":\"Bundle\",\"type\":\"searchset\",\"entry\":[{\"resource\":{\"resourceType\":\"Consent\",\"id\":\"" + id +
        "\",\"status\":\"active\",\"scope\":{\"coding\":[{\"system\":\"http://terminology.hl7.org/CodeSystem/consentscope\",\"code\":\"patient-privacy\"}]}," +
        "\"category\":[{\"coding\":[{\"system\":\"http://terminology.hl7.org/CodeSystem/v3-ActCode\",\"code\":\"INFA\"}]}]," +
        "\"provision\":{\"type\":\"" + provision + "\"}}}]}";

    // FHIR JSON forbids an empty array for a repeating element — a zero-match Bundle
    // omits `entry` entirely (this is exactly what real HAPI sends back); a naive
    // `"entry":[]` fixture makes Firely's strict deserializer throw, not just return
    // an empty list.
    private static string EmptyBundle() => """{"resourceType":"Bundle","type":"searchset"}""";

    private static string Everything() => """
        {"resourceType":"Bundle","type":"searchset","entry":[{"resource":{"resourceType":"Patient","id":"pat-1"}}]}
        """;

    [Fact]
    public async Task Unknown_national_id_is_a_404()
    {
        using var host = Host(new FakeHandler(_ => FhirJson(200, EmptyBundle())));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized(scope: "user/*.read").DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync($"/patients/{NationalId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_permitted_patient_returns_everything()
    {
        using var host = Host(new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/baseR4/Patient" => FhirJson(200, PatientBundle("pat-1")),
            "/baseR4/Consent" => FhirJson(200, ConsentBundle("consent-1", "permit")),
            "/baseR4/Patient/pat-1/$everything" => FhirJson(200, Everything()),
            _ => FhirJson(404, "{}"),
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized(scope: "user/*.read").DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync($"/patients/{NationalId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("pat-1");
    }

    [Fact]
    public async Task A_denied_patient_is_refused_with_403_and_an_OperationOutcome()
    {
        using var host = Host(new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/baseR4/Patient" => FhirJson(200, PatientBundle("pat-1")),
            "/baseR4/Consent" => FhirJson(200, ConsentBundle("consent-1", "deny")),
            _ => FhirJson(404, "{}"),
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized(scope: "user/*.read").DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync($"/patients/{NationalId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("OperationOutcome");
        body.ShouldContain("deny");
    }

    [Fact]
    public async Task Break_glass_without_the_scope_is_still_refused()
    {
        using var host = Host(new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/baseR4/Patient" => FhirJson(200, PatientBundle("pat-1")),
            "/baseR4/Consent" => FhirJson(200, ConsentBundle("consent-1", "deny")),
            _ => FhirJson(404, "{}"),
        }));
        using var client = host.CreateClient();
        // "user/*.read" only — no "break-glass" scope.
        client.DefaultRequestHeaders.Authorization = factory.Authorized(scope: "user/*.read").DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync($"/patients/{NationalId}?breakGlass=true");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Break_glass_with_the_scope_reads_anyway_and_audits_the_override()
    {
        var auditPosted = false;
        using var host = Host(new FakeHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/baseR4/AuditEvent")
            {
                auditPosted = true;
                return FhirJson(201, """{"resourceType":"AuditEvent","id":"audit-1"}""");
            }
            return request.RequestUri!.AbsolutePath switch
            {
                "/baseR4/Patient" => FhirJson(200, PatientBundle("pat-1")),
                "/baseR4/Consent" => FhirJson(200, ConsentBundle("consent-1", "deny")),
                "/baseR4/Patient/pat-1/$everything" => FhirJson(200, Everything()),
                _ => FhirJson(404, "{}"),
            };
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            factory.Authorized(scope: "user/*.read break-glass").DefaultRequestHeaders.Authorization;

        var response = await client.GetAsync($"/patients/{NationalId}?breakGlass=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        auditPosted.ShouldBeTrue();
    }

    [Fact]
    public async Task Toggling_consent_to_deny_updates_the_existing_resource()
    {
        string? putBody = null;
        using var host = Host(new FakeHandler(request =>
        {
            if (request.Method == HttpMethod.Put)
            {
                putBody = request.Content!.ReadAsStringAsync().Result;
                return FhirJson(200, putBody);
            }
            return FhirJson(200, ConsentBundle("consent-1", "permit"));
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized().DefaultRequestHeaders.Authorization;

        var response = await client.PutAsJsonAsync($"/patients/{NationalId}/consent", new { provision = "deny" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        putBody.ShouldNotBeNull();
        putBody.ShouldContain("\"deny\"");
    }

    [Fact]
    public async Task Toggling_consent_for_a_patient_with_none_on_file_is_a_404()
    {
        using var host = Host(new FakeHandler(_ => FhirJson(200, EmptyBundle())));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized().DefaultRequestHeaders.Authorization;

        var response = await client.PutAsJsonAsync($"/patients/{NationalId}/consent", new { provision = "deny" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
