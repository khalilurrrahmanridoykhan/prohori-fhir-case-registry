using System.Net;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
namespace Prohori.Api.Tests;
public class PatientWriteTests(AuthenticatedFactory factory) : IClassFixture<AuthenticatedFactory>
{
    private const string Patch = "{\"resourceType\":\"Parameters\",\"parameter\":[]}";
    [Fact]
    public async Task Patch_requires_authentication()
    {
        var response = await factory.CreateClient().PatchAsync("/cases/demo", new StringContent(Patch));
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task Patch_requires_version()
    {
        var response = await factory.Authorized().PatchAsync("/cases/demo", new StringContent(Patch, Encoding.UTF8, "application/fhir+json"));
        ((int)response.StatusCode).ShouldBe(428);
    }
    [Theory]
    [InlineData(200)]
    [InlineData(412)]
    [InlineData(409)]
    public async Task Patch_forwards_version_and_preserves_upstream_status(int status)
    {
        var handler = new CaptureHandler(status);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient("fhir").ConfigurePrimaryHttpMessageHandler(() => handler)));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = factory.Authorized().DefaultRequestHeaders.Authorization;
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/cases/demo")
        { Content = new StringContent(Patch, Encoding.UTF8, "application/fhir+json") };
        request.Headers.TryAddWithoutValidation("If-Match", "W/\"1\"");
        var response = await client.SendAsync(request);
        ((int)response.StatusCode).ShouldBe(status == 409 ? 412 : status);
        handler.Version.ShouldBe("W/\"1\"");
        handler.Path.ShouldBe("/baseR4/Patient/demo");
        handler.Body.ShouldBe(Patch);
    }
    private sealed class CaptureHandler(int status) : HttpMessageHandler
    {
        public string Version, Path, Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Version = request.Headers.GetValues("If-Match").Single();
            Path = request.RequestUri.AbsolutePath;
            Body = await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(status == 409 ? "{\"resourceType\":\"OperationOutcome\",\"issue\":[{\"diagnostics\":\"HAPI-0974: stale version\"}]}" : "{}", Encoding.UTF8, "application/fhir+json") };
        }
    }
}
