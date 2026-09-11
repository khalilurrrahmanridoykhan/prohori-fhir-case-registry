using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Prohori.Api.Tests;

/// <summary>
/// In-process tests of the SDC HTTP surface. These exercise request binding, extraction and
/// authorization only — <c>$extract</c> without <c>?dryRun=true</c> would still reach a real
/// FHIR server, so that path is left to the integration tests.
/// </summary>
public class QuestionnaireEndpointTests(AuthenticatedFactory factory)
    : IClassFixture<AuthenticatedFactory>
{
    private readonly HttpClient _client = factory.Authorized();

    [Fact]
    public async Task Questionnaire_is_public_and_returns_the_canonical_definition()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync("/questionnaire-response/questionnaire");
        var body = await response.Content.ReadFromJsonAsync<QuestionnaireBody>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body!.ResourceType.ShouldBe("Questionnaire");
        body.Url.ShouldBe("https://prohori.health/fhir/Questionnaire/prohori-case-questionnaire");
    }

    [Fact]
    public async Task Extract_without_a_bearer_token_is_refused()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/questionnaire-response/$extract?dryRun=true", ValidResponseJson());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Populate_without_a_bearer_token_is_refused()
    {
        var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/questionnaire-response/$populate", new { nationalId = "19942691012345678" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DryRun_extract_returns_the_case_Bundle_without_submitting_it()
    {
        var response = await _client.PostAsJsonAsync("/questionnaire-response/$extract?dryRun=true", ValidResponseJson());
        var bundle = await response.Content.ReadFromJsonAsync<BundleBody>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/fhir+json");
        bundle!.ResourceType.ShouldBe("Bundle");
        bundle.Type.ShouldBe("transaction");
        bundle.Entry.Select(e => e.Resource.ResourceType)
            .ShouldBe(["Patient", "Encounter", "Observation", "Condition"]);
    }

    [Fact]
    public async Task Malformed_JSON_is_a_400_not_a_500()
    {
        var response = await _client.PostAsync("/questionnaire-response/$extract",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_response_missing_required_answers_is_a_400_with_field_errors()
    {
        var incomplete = new
        {
            resourceType = "QuestionnaireResponse",
            status = "completed",
            item = new object[]
            {
                new { linkId = "disease", answer = new[] { new { valueCoding = new { system = "http://snomed.info/sct", code = "38362002" } } } },
            },
        };

        var response = await _client.PostAsJsonAsync("/questionnaire-response/$extract?dryRun=true", incomplete);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!.Errors.Keys.ShouldContain("patient.nationalId");
    }

    private static object ValidResponseJson() => new
    {
        resourceType = "QuestionnaireResponse",
        status = "completed",
        item = new object[]
        {
            new
            {
                linkId = "patient",
                item = new object[]
                {
                    new { linkId = "patient.nationalId", answer = new[] { new { valueString = Sample.FreshNationalId() } } },
                    new { linkId = "patient.familyName", answer = new[] { new { valueString = "Khan" } } },
                    new { linkId = "patient.givenNames", answer = new[] { new { valueString = "Rahman" } } },
                    new { linkId = "patient.gender", answer = new[] { new { valueCoding = new { system = "http://hl7.org/fhir/administrative-gender", code = "male" } } } },
                    new { linkId = "patient.birthDate", answer = new[] { new { valueDate = "1995-06-15" } } },
                    new { linkId = "patient.city", answer = new[] { new { valueString = "Dhaka" } } },
                    new { linkId = "patient.district", answer = new[] { new { valueString = "Dhaka" } } },
                },
            },
            new { linkId = "disease", answer = new[] { new { valueCoding = new { system = "http://snomed.info/sct", code = "38362002" } } } },
            new { linkId = "rdtResult", answer = new[] { new { valueCoding = new { system = "http://snomed.info/sct", code = "10828004" } } } },
            new { linkId = "visitDate", answer = new[] { new { valueDateTime = "2026-08-14T09:20:00+06:00" } } },
        },
    };

    private sealed record QuestionnaireBody(string ResourceType, string Url);
    private sealed record BundleBody(string ResourceType, string Type, BundleEntry[] Entry);
    private sealed record BundleEntry(BundleResource Resource);
    private sealed record BundleResource(string ResourceType);
    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}
