using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Prohori.Api.Tests;

/// <summary>
/// In-process tests of the HTTP surface. These exercise request binding and
/// validation only — they never reach a FHIR server (that is covered by the
/// Integration tests).
/// </summary>
public class CasesEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_reports_ok_and_the_target_server()
    {
        var body = await _client.GetFromJsonAsync<HealthResponse>("/health");

        body.ShouldNotBeNull();
        body.Status.ShouldBe("ok");
        body.FhirBaseUrl.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Missing_national_id_is_a_400_with_a_field_error()
    {
        var bad = Sample.Case() with
        {
            Patient = Sample.Case().Patient with { NationalId = "" },
        };

        var response = await _client.PostAsJsonAsync("/cases", bad);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        problem!.Errors.Keys.ShouldContain(k => k.Contains("NationalId"));
    }

    [Fact]
    public async Task Non_digit_national_id_is_rejected()
    {
        var bad = Sample.Case() with
        {
            Patient = Sample.Case().Patient with { NationalId = "not-a-number" },
        };

        var response = await _client.PostAsJsonAsync("/cases", bad);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_gender_is_rejected()
    {
        var bad = Sample.Case() with
        {
            Patient = Sample.Case().Patient with { Gender = "banana" },
        };

        var response = await _client.PostAsJsonAsync("/cases", bad);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record HealthResponse(string Status, string FhirBaseUrl);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
