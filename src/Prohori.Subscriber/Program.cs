using System.Text;
using Hl7.Fhir.Serialization;
using Prohori.Subscriber;

var builder = WebApplication.CreateBuilder(args);

// Same convention as Prohori.Api / Prohori.V2Gateway: Fhir:BaseUrl / Fhir__BaseUrl.
var fhirBaseUrl = (builder.Configuration["Fhir:BaseUrl"] ?? "http://localhost:8080/fhir").TrimEnd('/') + "/";
// The URL HAPI (which may be in a different container/host) must be able to reach
// THIS service at, to deliver the rest-hook callback. In Docker, that's usually
// http://host.docker.internal:<port> — see deploy/docker-compose.yml's `extra_hosts`.
var publicUrl = (builder.Configuration["Subscriber:PublicUrl"] ?? "http://localhost:5300").TrimEnd('/');
// A shared secret in the callback URL, not a bearer token: HAPI's rest-hook client
// doesn't carry OAuth2 credentials, so this is the lightweight alternative to a
// fully-open unauthenticated receiver — same "documented trust boundary" pattern
// Prohori.V2Gateway's unauthenticated /adt uses for its own (different) reason.
var notifyKey = builder.Configuration["Subscriber:NotifyKey"] ?? "dev-only-key";

builder.Services.AddSingleton<NotificationBroadcaster>();
builder.Services.AddHttpClient("fhir", client => client.BaseAddress = new Uri(fhirBaseUrl));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", fhirBaseUrl, publicUrl }));

async Task<IResult> Notify(HttpRequest request, NotificationBroadcaster broadcaster)
{
    if (request.Headers["X-Notify-Key"] != notifyKey) return Results.Unauthorized();

    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    broadcaster.Publish(NotificationParser.ToDashboardPayload(body));
    return Results.Ok();
}

const string NotifySummary = "The rest-hook callback HAPI's Subscription delivers to (PUT .../{Type}/{id} when a payload is configured; POST to the bare path for an empty-payload ping). Broadcasts a small summary to every connected /stream client.";

// Two routes, not one optional one: HAPI's rest-hook delivery, with Payload set, PUTs to
// {endpoint}/{ResourceType}/{id} — it treats the Subscription's endpoint as a FHIR base
// URL, not a fixed webhook address, and a catch-all segment can't match a bare "/notify"
// with nothing after it.
app.MapPost("/notify", Notify).WithSummary(NotifySummary);
app.MapMethods("/notify/{**rest}", ["PUT", "POST"], Notify).WithSummary(NotifySummary);

app.MapGet("/stream", async (HttpContext context, NotificationBroadcaster broadcaster) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers["X-Accel-Buffering"] = "no"; // don't let a reverse proxy hold events back

    var id = broadcaster.Subscribe(out var reader);
    try
    {
        await context.Response.WriteAsync(": connected\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);

        await foreach (var payload in reader.ReadAllAsync(context.RequestAborted))
        {
            await context.Response.WriteAsync($"data: {payload}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
    }
    catch (OperationCanceledException)
    {
        // The tab closed or navigated away — not an error.
    }
    finally
    {
        broadcaster.Unsubscribe(id);
    }
})
.WithSummary("Server-Sent Events stream the dashboard subscribes to for live updates.");

app.MapGet("/debug/last-notification", (NotificationBroadcaster broadcaster) =>
    broadcaster.LastPayload is null ? Results.NotFound() : Results.Text(broadcaster.LastPayload, "application/json"))
    .WithSummary("For CI/local verification only — the last payload broadcast, without needing to hold an SSE connection open.");

// Register the Subscription once at startup. Logged, not fatal, if the FHIR
// server isn't reachable yet — this service should still come up and serve
// /health so compose/CI startup ordering has something to poll.
using (var scope = app.Services.CreateScope())
{
    var clients = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SubscriptionRegistrar");
    try
    {
        using var http = clients.CreateClient("fhir");
        var subscription = SubscriptionBuilder.Build($"{publicUrl}/notify", notifyKey);
        using var content = new StringContent(subscription.ToJson(), Encoding.UTF8, "application/fhir+json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "Subscription") { Content = content };
        request.Headers.TryAddWithoutValidation("If-None-Exist", $"criteria={Uri.EscapeDataString(SubscriptionBuilder.Criteria)}");
        using var response = await http.SendAsync(request);
        logger.LogInformation("Subscription registration: {Status} ({Reused})",
            response.StatusCode, response.StatusCode == System.Net.HttpStatusCode.OK ? "reused existing" : "created");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not register the Subscription at startup — the FHIR server may not be up yet.");
    }
}

app.Run();

/// <summary>Exposed so WebApplicationFactory&lt;Program&gt; can host this in tests.</summary>
public partial class Program;
