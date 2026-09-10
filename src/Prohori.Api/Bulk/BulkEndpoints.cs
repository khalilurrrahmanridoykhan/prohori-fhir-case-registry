using Microsoft.AspNetCore.Http.Extensions;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace Prohori.Api.Bulk;

public static partial class BulkEndpoints
{
    [GeneratedRegex("^[A-Za-z0-9.-]{1,64}$")]
    private static partial Regex FhirId();
    private static readonly HashSet<string> Parameters = ["_type", "_since", "_typeFilter", "_outputFormat"];
    private static string Owner(ClaimsPrincipal user) => user.FindFirstValue("sub")!;

    public static void MapBulk(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Bulk:Enabled")) return;
        var tokenEndpoint = (app.Configuration["Auth:Authority"] ?? "http://localhost:8091/realms/prohori-bulk").TrimEnd('/') + "/protocol/openid-connect/token";
        app.MapGet("/bulk/fhir/.well-known/smart-configuration", () => Results.Ok(new
        {
            token_endpoint = tokenEndpoint,
            token_endpoint_auth_methods_supported = new[] { "private_key_jwt" },
            token_endpoint_auth_signing_alg_values_supported = new[] { "RS384" },
            grant_types_supported = new[] { "client_credentials" },
            scopes_supported = new[] { "system/*.read" },
            capabilities = new[] { "permission-v1", "client-confidential-asymmetric" }
        }));
        app.MapGet("/bulk/fhir/$export", (HttpContext context, IHttpClientFactory clients, BulkJobStore jobs) => Kickoff(context, clients, jobs, "$export"))
            .RequireAuthorization("BulkRead");
        app.MapGet("/bulk/fhir/Patient/$export", (HttpContext context, IHttpClientFactory clients, BulkJobStore jobs) => Kickoff(context, clients, jobs, "Patient/$export"))
            .RequireAuthorization("BulkRead");
        app.MapGet("/bulk/fhir/Group/{id}/$export", (string id, HttpContext context, IHttpClientFactory clients, BulkJobStore jobs) =>
            !FhirId().IsMatch(id) ? Task.FromResult<IResult>(Results.BadRequest()) : Kickoff(context, clients, jobs, $"Group/{id}/$export"))
            .RequireAuthorization("BulkRead");
        app.MapGet("/bulk/jobs/{id}", Poll).RequireAuthorization("BulkRead");
        app.MapGet("/bulk/jobs/{id}/files/{fileId}", Download).RequireAuthorization("BulkRead");
    }

    private static async Task<IResult> Kickoff(HttpContext context, IHttpClientFactory clients, BulkJobStore jobs, string path)
    {
        var query = context.Request.Query;
        if (!context.Request.Headers["Prefer"].ToString().Split(',').Any(x => x.Trim() == "respond-async"))
            return Results.Problem(statusCode: 400, detail: "Bulk export requires Prefer: respond-async.");
        if (query.Keys.Any(key => !Parameters.Contains(key)) || query.Any(x => x.Key != "_typeFilter" && x.Value.Count != 1))
            return Results.Problem(statusCode: 400, detail: "Unsupported or repeated export parameter.");
        if (query.TryGetValue("_since", out var since) && !DateTimeOffset.TryParse(since, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
            return Results.Problem(statusCode: 400, detail: "_since must be an ISO timestamp.");
        if (query.TryGetValue("_outputFormat", out var format) && format != "application/fhir+ndjson")
            return Results.Problem(statusCode: 400, detail: "Use application/fhir+ndjson.");
        if (query.TryGetValue("_type", out var types) && types.ToString().Split(',').Any(t => t is not ("Patient" or "Encounter" or "Observation" or "Condition")))
            return Results.Problem(statusCode: 400, detail: "Supported types: Patient,Encounter,Observation,Condition.");
        var values = query.SelectMany(x => x.Value.Select(value => new KeyValuePair<string, string?>(x.Key, value))).ToList();
        if (!query.ContainsKey("_type")) values.Add(new("_type", "Patient,Encounter,Observation,Condition"));
        if (!query.ContainsKey("_outputFormat")) values.Add(new("_outputFormat", "application/fhir+ndjson"));
        using var http = clients.CreateClient("fhir");
        using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString(path, values));
        request.Headers.TryAddWithoutValidation("Prefer", "respond-async");
        request.Headers.TryAddWithoutValidation("Accept", "application/fhir+json");
        // Each caller receives its own upstream job instead of reusing HAPI's cached export.
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
        try
        {
            using var response = await http.SendAsync(request, context.RequestAborted);
            if ((int)response.StatusCode != 202) return await Error(response, context.RequestAborted);
            var location = response.Content.Headers.ContentLocation?.ToString();
            if (!TryUpstreamUrl(http.BaseAddress!, location, out var statusUrl) || statusUrl!.AbsolutePath != http.BaseAddress!.AbsolutePath + "$export-poll-status")
                return Results.Problem(statusCode: 502, detail: "FHIR server returned an invalid polling URL.");
            var job = jobs.Add(Owner(context.User), statusUrl, context.Request.GetEncodedUrl());
            if (job == null) return Results.Problem(statusCode: 503, detail: "Export registry is full; retry after jobs expire.");
            context.Response.Headers.ContentLocation = new Uri(new Uri(context.Request.GetEncodedUrl()), $"/bulk/jobs/{job.Id}").AbsoluteUri;
            context.Response.Headers.RetryAfter = "2";
            return Results.StatusCode(202);
        }
        catch (HttpRequestException) { return Results.Problem(statusCode: 502, detail: "FHIR export server unavailable."); }
    }

    private static async Task<IResult> Poll(string id, HttpContext context, IHttpClientFactory clients, BulkJobStore jobs)
    {
        var job = jobs.Find(id, Owner(context.User));
        if (job == null) return Results.NotFound();
        using var http = clients.CreateClient("fhir");
        try
        {
            using var response = await http.GetAsync(job.StatusUrl, context.RequestAborted);
            if ((int)response.StatusCode == 202)
            {
                context.Response.Headers.RetryAfter = response.Headers.RetryAfter?.ToString() ?? "2";
                return Results.StatusCode(202);
            }
            if (!response.IsSuccessStatusCode) return await Error(response, context.RequestAborted);
            var manifest = JsonNode.Parse(await response.Content.ReadAsStringAsync(context.RequestAborted))?.AsObject()
                ?? throw new FormatException("Missing manifest.");
            if (manifest["output"] is not JsonArray) throw new FormatException("Missing output array.");
            var files = new Dictionary<string, Uri>();
            foreach (var field in new[] { "output", "error", "deleted" })
            {
                if (manifest[field] is not JsonArray entries) continue;
                foreach (var entry in entries)
                {
                    if (!TryUpstreamUrl(http.BaseAddress!, entry?["url"]?.GetValue<string>(), out var fileUrl)
                        || !fileUrl!.AbsolutePath.StartsWith(http.BaseAddress!.AbsolutePath + "Binary/", StringComparison.Ordinal)
                        || !FhirId().IsMatch(fileUrl.AbsolutePath[(http.BaseAddress.AbsolutePath.Length + 7)..]) || fileUrl.Query.Length != 0)
                        throw new FormatException("Invalid export file URL.");
                    var fileId = files.Count.ToString(CultureInfo.InvariantCulture);
                    files.Add(fileId, fileUrl);
                    entry!["url"] = new Uri(new Uri(job.RequestUrl), $"/bulk/jobs/{id}/files/{fileId}").AbsoluteUri;
                }
            }
            job.Files = files;
            manifest["requiresAccessToken"] = true;
            // Do not publish internal hostnames in the manifest.
            manifest["request"] = job.RequestUrl;
            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(manifest);
        }
        catch (HttpRequestException) { return Results.Problem(statusCode: 502, detail: "FHIR export server unavailable."); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException)
        { return Results.Problem(statusCode: 502, detail: "FHIR server returned an invalid export manifest."); }
    }

    private static async Task Download(string id, string fileId, HttpContext context, IHttpClientFactory clients, BulkJobStore jobs)
    {
        var job = jobs.Find(id, Owner(context.User));
        if (job == null || !job.Files.TryGetValue(fileId, out var url)) { context.Response.StatusCode = 404; return; }
        using var http = clients.CreateClient("fhir");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/fhir+ndjson");
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/fhir+ndjson";
            context.Response.Headers.CacheControl = "no-store";
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (HttpRequestException) when (!context.Response.HasStarted) { context.Response.StatusCode = 502; }
    }

    public static bool TryUpstreamUrl(Uri baseUrl, string? location, out Uri? result)
    {
        result = null;
        return !string.IsNullOrEmpty(location) && Uri.TryCreate(baseUrl, location, out result)
            && result.Scheme == baseUrl.Scheme && result.Authority == baseUrl.Authority && result.UserInfo.Length == 0
            && result.Fragment.Length == 0 && result.AbsolutePath.StartsWith(baseUrl.AbsolutePath, StringComparison.Ordinal);
    }
    private static async Task<IResult> Error(HttpResponseMessage response, CancellationToken cancellation) =>
        Results.Text(await response.Content.ReadAsStringAsync(cancellation), response.Content.Headers.ContentType?.ToString() ?? "application/fhir+json",
            statusCode: response.IsSuccessStatusCode ? 502 : (int)response.StatusCode);
}
