using System.Net;
using System.Security.Cryptography;
using System.Text;
using Prohori.BulkClient;

namespace Prohori.Api.Tests;

public class BulkClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
    private static HttpResponseMessage Response(HttpStatusCode code, string body = "", string type = "application/json") =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, type) };

    [Theory]
    [InlineData("https://evil.test/file")]
    [InlineData("http://localhost:9999/file")]
    [InlineData("http://user@localhost/file")]
    [InlineData("http://localhost/file#fragment")]
    public void Untrusted_urls_are_rejected(string url) =>
        Should.Throw<InvalidOperationException>(() => BulkExport.TrustedUrl(new Uri("http://localhost/"), url));

    [Fact]
    public async Task Real_protocol_sequence_caches_token_and_downloads_ndjson()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        using var key = RSA.Create(2048);
        var calls = new List<string>();
        var pollCount = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            calls.Add(path);
            if (path == "/token")
            {
                request.Method.ShouldBe(HttpMethod.Post);
                var form = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                form.ShouldContain("grant_type=client_credentials");
                form.ShouldContain("client_assertion=");
                return Response(HttpStatusCode.OK, """{"access_token":"test-token","token_type":"Bearer","scope":"system/*.read","expires_in":300}""");
            }
            request.Headers.Authorization!.Parameter.ShouldBe("test-token");
            if (path.EndsWith("$export"))
            {
                request.Headers.GetValues("Prefer").Single().ShouldBe("respond-async");
                var response = Response(HttpStatusCode.Accepted);
                response.Content.Headers.ContentLocation = new Uri("http://localhost/jobs/one");
                return response;
            }
            if (path == "/jobs/one" && pollCount++ == 0) return Response(HttpStatusCode.Accepted);
            if (path == "/jobs/one") return Response(HttpStatusCode.OK,
                """{"transactionTime":"2026-09-09T00:00:00Z","request":"http://localhost/$export","requiresAccessToken":true,"output":[{"type":"Patient","url":"http://localhost/file"}],"error":[]}""");
            return Response(HttpStatusCode.OK, "{\"resourceType\":\"Patient\",\"id\":\"one\"}\n", "application/fhir+ndjson");
        }));
        try
        {
            var options = ExportOptions.Parse(["--base-url", "http://localhost/", "--output", directory, "--poll-seconds", "1"]);
            var auth = new BackendAuthentication(http, "backend", new Uri("http://localhost/token"), key);
            var (manifest, run) = await new BulkExport(http, auth, options).Run(default);
            calls.Count(p => p == "/token").ShouldBe(1);
            pollCount.ShouldBe(2);
            manifest.Output.Length.ShouldBe(1);
            File.ReadAllText(Path.Combine(run, "0000.ndjson")).ShouldContain("Patient");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Token_without_granted_system_scope_is_rejected()
    {
        using var key = RSA.Create(2048);
        using var http = new HttpClient(new Handler(_ => Response(HttpStatusCode.OK,
            """{"access_token":"test-token","token_type":"Bearer","scope":"user/*.read","expires_in":300}""")));
        var auth = new BackendAuthentication(http, "backend", new Uri("http://localhost/token"), key);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/$export");
        await Should.ThrowAsync<InvalidOperationException>(() => auth.Authorize(request, default));
        request.Headers.Authorization.ShouldBeNull();
    }

    [Theory]
    [InlineData("error")]
    [InlineData("deleted")]
    public async Task Partial_or_deletion_manifests_never_create_a_snapshot(string field)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        using var key = RSA.Create(2048);
        using var http = new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/token") return Response(HttpStatusCode.OK,
                """{"access_token":"test-token","token_type":"Bearer","scope":"system/*.read","expires_in":300}""");
            if (request.RequestUri.AbsolutePath.EndsWith("$export"))
            {
                var response = Response(HttpStatusCode.Accepted);
                response.Content.Headers.ContentLocation = new Uri("http://localhost/job");
                return response;
            }
            return Response(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["transactionTime"] = "2026-09-09T00:00:00Z", ["request"] = "http://localhost/$export",
                ["requiresAccessToken"] = true, ["output"] = Array.Empty<object>(), ["error"] = Array.Empty<object>(),
                [field] = new[] { new { type = "OperationOutcome", url = "http://localhost/errors" } }
            }));
        }));
        var options = ExportOptions.Parse(["--base-url", "http://localhost/", "--output", directory, "--poll-seconds", "1"]);
        var auth = new BackendAuthentication(http, "backend", new Uri("http://localhost/token"), key);
        await Should.ThrowAsync<InvalidOperationException>(() => new BulkExport(http, auth, options).Run(default));
        Directory.Exists(directory).ShouldBeFalse();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"transactionTime\":\"2026-09-09T00:00:00Z\",\"request\":\"x\",\"requiresAccessToken\":true,\"output\":null,\"error\":[]}")]
    public void Incomplete_manifest_fails(string json) => Should.Throw<InvalidOperationException>(() => BulkExport.ParseManifest(json));
}
