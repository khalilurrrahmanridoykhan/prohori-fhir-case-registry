using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prohori.Api.Bulk;

namespace Prohori.Api.Tests;

public class BulkGatewayTests(AuthenticatedFactory factory) : IClassFixture<AuthenticatedFactory>
{
    private WebApplicationFactory<Program> Host(HttpMessageHandler handler) => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string> { ["Bulk:Enabled"] = "true" }));
        builder.ConfigureTestServices(services => services.AddHttpClient("fhir").ConfigurePrimaryHttpMessageHandler(() => handler));
    });
    private HttpClient Client(WebApplicationFactory<Program> host, string scope = "system/*.read", string subject = "backend-one")
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized(scope, subject: subject).DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.TryAddWithoutValidation("Prefer", "respond-async");
        return client;
    }
    [Theory]
    [InlineData("/bulk/fhir/$export")]
    [InlineData("/bulk/fhir/Patient/$export")]
    [InlineData("/bulk/fhir/Group/demo/$export")]
    [InlineData("/bulk/jobs/other")]
    [InlineData("/bulk/jobs/other/files/0")]
    public async Task Every_export_surface_requires_a_token_and_exact_system_scope(string path)
    {
        var handler = new FakeHandler(_ => throw new Exception("No upstream call expected."));
        using var host = Host(handler);
        (await host.CreateClient().GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Client(host, "user/*.read").GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client(host, "system/*.read.extra").GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task Job_and_files_belong_to_the_client_and_manifest_urls_are_protected()
    {
        var handler = new FakeHandler(request =>
        {
            request.Headers.Authorization.ShouldBeNull(); // Keycloak token must not leak to HAPI.
            if (request.RequestUri.AbsolutePath.EndsWith("/$export"))
            {
                var result = Json(202, "");
                result.Content.Headers.ContentLocation = new Uri("https://hapi.fhir.org/baseR4/$export-poll-status?_jobId=one");
                return result;
            }
            if (request.RequestUri.AbsolutePath.EndsWith("$export-poll-status"))
                return Json(200, """{"transactionTime":"2026-09-09T10:00:00Z","request":"internal","requiresAccessToken":false,"output":[{"type":"Patient","url":"https://hapi.fhir.org/baseR4/Binary/file-one"}],"error":[]}""");
            request.RequestUri.AbsolutePath.ShouldBe("/baseR4/Binary/file-one");
            return Json(200, "{\"resourceType\":\"Patient\",\"id\":\"demo\"}\n", "application/fhir+ndjson");
        });
        using var host = Host(handler);
        using var client = Client(host);
        var response = await client.GetAsync("/bulk/fhir/Group/demo/$export");
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var location = response.Content.Headers.ContentLocation!;
        (await Client(host, subject: "other-backend").GetAsync(location)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var poll = await client.GetAsync(location);
        using var manifest = JsonDocument.Parse(await poll.Content.ReadAsStringAsync());
        manifest.RootElement.GetProperty("requiresAccessToken").GetBoolean().ShouldBeTrue();
        manifest.RootElement.GetProperty("request").GetString().ShouldBe("http://localhost/bulk/fhir/Group/demo/$export");
        var file = manifest.RootElement.GetProperty("output")[0].GetProperty("url").GetString();
        (await Client(host, subject: "other-backend").GetAsync(file)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync(file)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
    [Fact]
    public async Task Untrusted_polling_location_is_rejected()
    {
        using var host = Host(new FakeHandler(_ =>
        {
            var response = Json(202, "");
            response.Content.Headers.ContentLocation = new Uri("https://attacker.invalid/poll");
            return response;
        }));
        (await Client(host).GetAsync("/bulk/fhir/$export")).StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }
    [Theory]
    [InlineData("https://attacker.invalid/Binary/one")]
    [InlineData("https://hapi.fhir.org/baseR4/Patient/one")]
    [InlineData("https://hapi.fhir.org/baseR4/Binary/one?secret=1")]
    public async Task Untrusted_or_non_binary_manifest_files_are_rejected(string file)
    {
        using var host = Host(new FakeHandler(_ => Json(200, JsonSerializer.Serialize(new
        { output = new[] { new { type = "Patient", url = file } }, error = Array.Empty<object>() }))));
        var job = host.Services.GetRequiredService<BulkJobStore>().Add("backend-one", new Uri("https://hapi.fhir.org/baseR4/$export-poll-status"), "http://localhost/bulk/fhir/$export")!;
        (await Client(host).GetAsync($"/bulk/jobs/{job.Id}")).StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    }
    [Theory]
    [InlineData("?_since=not-a-date")]
    [InlineData("?_type=Binary")]
    [InlineData("?url=https://attacker.invalid")]
    public async Task Invalid_export_arguments_do_not_reach_HAPI(string query)
    {
        using var host = Host(new FakeHandler(_ => throw new Exception("No upstream call expected.")));
        (await Client(host).GetAsync("/bulk/fhir/$export" + query)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
    private static HttpResponseMessage Json(int status, string body, string type = "application/json") =>
        new((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, type) };
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
}
