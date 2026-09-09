using System.Net;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Json;
namespace Prohori.Api.Tests;
public class AuthorizationTests(AuthenticatedFactory factory) : IClassFixture<AuthenticatedFactory>
{
    [Fact]
    public async Task Production_without_configured_identity_provider_still_serves_health_and_refuses_writes()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = host.CreateClient();
        (await client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/cases", Sample.Case())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
    [Theory]
    [InlineData("/cases")]
    [InlineData("/bd-core/cases")]
    public async Task No_token_is_401(string path) =>
        (await factory.CreateClient().PostAsJsonAsync(path, Sample.Case())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    [Theory]
    [InlineData("patient/*.rs")]
    [InlineData("user/*.write.extra")]
    [InlineData("")]
    public async Task Wrong_scope_is_403(string scope) =>
        (await factory.Authorized(scope).PostAsJsonAsync("/cases", Sample.Case())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    [Fact]
    public async Task Wrong_audience_is_401() =>
        (await factory.Authorized(audience: "other").PostAsJsonAsync("/cases", Sample.Case())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    [Fact]
    public async Task Expired_token_is_401() =>
        (await factory.Authorized(expired: true).PostAsJsonAsync("/cases", Sample.Case())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
}
