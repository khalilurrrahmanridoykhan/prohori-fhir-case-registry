using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Prohori.Api.Tests;

/// <summary>
/// In-process tests of <c>POST /legacy-import/cases</c>, with a fake "fhir" HttpClient standing
/// in for the terminology server's $translate — the real, live version is proven in
/// scripts/verify-terminology.sh and TerminologyClientTests covers response parsing.
/// </summary>
public class LegacyImportEndpointTests(AuthenticatedFactory factory) : IClassFixture<AuthenticatedFactory>
{
    private WebApplicationFactory<Program> Host(HttpMessageHandler handler) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => services.AddHttpClient("fhir").ConfigurePrimaryHttpMessageHandler(() => handler)));

    private static HttpResponseMessage Json(int status, string body) =>
        new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }

    private static object LegacyCase(string rdtResultLegacy = "pos", string nationalId = "19942691012345678") => new
    {
        patient = new
        {
            nationalId, familyName = "Khan", givenNames = new[] { "Rahman" },
            gender = "male", birthDate = "1995-06-15", city = "Dhaka", district = "Dhaka",
        },
        disease = "dengue",
        rdtResultLegacy,
        visitDate = "2026-08-14T09:20:00+06:00",
    };

    private const string TranslatesToPositive = """
        { "resourceType": "Parameters", "parameter": [
          { "name": "result", "valueBoolean": true },
          { "name": "match", "part": [{ "name": "concept", "valueCoding": { "system": "http://snomed.info/sct", "code": "10828004" } }] }
        ] }
        """;
    private const string NoTranslation = """{ "resourceType": "Parameters", "parameter": [{ "name": "result", "valueBoolean": false }] }""";

    [Fact]
    public async Task Without_a_bearer_token_the_request_is_refused_and_translate_is_never_called()
    {
        using var host = Host(new FakeHandler(_ => throw new Exception("No upstream call expected.")));

        var response = await host.CreateClient().PostAsJsonAsync("/legacy-import/cases", LegacyCase());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_legacy_code_is_a_400_before_any_translate_call()
    {
        using var host = Host(new FakeHandler(_ => throw new Exception("No upstream call expected.")));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized().DefaultRequestHeaders.Authorization;

        var response = await client.PostAsJsonAsync("/legacy-import/cases", LegacyCase(rdtResultLegacy: "unknown"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_untranslatable_code_is_a_502_naming_the_missing_ConceptMap()
    {
        using var host = Host(new FakeHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("$translate") ? Json(200, NoTranslation) : throw new Exception("Unexpected call.")));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized().DefaultRequestHeaders.Authorization;

        var response = await client.PostAsJsonAsync("/legacy-import/cases", LegacyCase());
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        body.ShouldContain("load-terminology.sh");
    }

    [Fact]
    public async Task A_translated_code_builds_the_same_Bundle_shape_the_typed_endpoint_does()
    {
        // dryRun stops short of the actual FHIR transaction (covered by CaseBundleBuilderTests
        // and the live Integration tests) — this proves the translated result reaches the builder.
        using var host = Host(new FakeHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("$translate") ? Json(200, TranslatesToPositive) : throw new Exception("Unexpected call.")));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized().DefaultRequestHeaders.Authorization;

        var response = await client.PostAsJsonAsync("/legacy-import/cases?dryRun=true", LegacyCase());
        var bundle = await response.Content.ReadFromJsonAsync<BundleBody>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        bundle!.ResourceType.ShouldBe("Bundle");
        bundle.Entry.Select(e => e.Resource.ResourceType)
            .ShouldBe(["Patient", "Encounter", "Observation", "Condition"]);
        var observation = bundle.Entry.Single(e => e.Resource.ResourceType == "Observation").Resource;
        observation.ValueCodeableConcept!.Coding.Single().Code.ShouldBe("10828004"); // the translated code, not "pos"
    }

    private sealed record BundleBody(string ResourceType, BundleEntry[] Entry);
    private sealed record BundleEntry(BundleResource Resource);
    private sealed record BundleResource(string ResourceType, ValueCodeableConcept? ValueCodeableConcept);
    private sealed record ValueCodeableConcept(BundleCoding[] Coding);
    private sealed record BundleCoding(string Code);
}
