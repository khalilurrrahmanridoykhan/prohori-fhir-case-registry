using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Prohori.BulkClient;

public sealed record ExportFile(string Type, string Url);
public sealed record ExportManifest(DateTimeOffset TransactionTime, string Request, bool RequiresAccessToken,
    ExportFile[] Output, ExportFile[] Error, ExportFile[]? Deleted = null);

public sealed class BulkExport(HttpClient http, BackendAuthentication authentication, ExportOptions options)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<(ExportManifest Manifest, string Directory)> Run(CancellationToken cancellation)
    {
        using var kickoff = new HttpRequestMessage(HttpMethod.Get, options.Kickoff());
        kickoff.Headers.TryAddWithoutValidation("Prefer", "respond-async");
        kickoff.Headers.TryAddWithoutValidation("Accept", "application/fhir+json");
        await authentication.Authorize(kickoff, cancellation);
        using var accepted = await http.SendAsync(kickoff, cancellation);
        if (accepted.StatusCode != HttpStatusCode.Accepted) throw await Failure("Export kickoff", accepted, cancellation);
        var status = TrustedUrl(options.BaseUrl, accepted.Content.Headers.ContentLocation?.ToString());
        Console.WriteLine($"Export accepted. Polling {status.GetLeftPart(UriPartial.Path)}");
        var delay = RetryDelay(accepted, options.PollSeconds);
        ExportManifest manifest;
        while (true)
        {
            await Task.Delay(delay, cancellation);
            using var poll = new HttpRequestMessage(HttpMethod.Get, status);
            await authentication.Authorize(poll, cancellation);
            using var response = await http.SendAsync(poll, cancellation);
            if (response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                delay = RetryDelay(response, options.PollSeconds);
                continue;
            }
            if (response.StatusCode != HttpStatusCode.OK) throw await Failure("Export polling", response, cancellation);
            manifest = ParseManifest(await response.Content.ReadAsStringAsync(cancellation));
            break;
        }
        if (manifest.Error.Length != 0) throw new InvalidOperationException("The export manifest reports errors; refusing to update local state.");
        if (manifest.Deleted?.Length > 0) throw new InvalidOperationException("Deletion manifests are not supported; use a fresh full snapshot.");
        var run = Path.Combine(options.Output, "runs", DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(run);
        await File.WriteAllTextAsync(Path.Combine(run, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions), cancellation);
        for (var i = 0; i < manifest.Output.Length; i++)
        {
            var file = manifest.Output[i];
            var url = TrustedUrl(options.BaseUrl, file.Url);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/fhir+ndjson");
            if (manifest.RequiresAccessToken) await authentication.Authorize(request, cancellation);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (response.StatusCode != HttpStatusCode.OK) throw await Failure("NDJSON download", response, cancellation);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not ("application/fhir+ndjson" or "application/ndjson")) throw new InvalidOperationException($"Expected NDJSON, received {mediaType}.");
            // Server-supplied resource names and URLs never become filesystem paths.
            var output = Path.Combine(run, $"{i:D4}.ndjson");
            await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            await response.Content.CopyToAsync(stream, cancellation);
        }
        Console.WriteLine(JsonSerializer.Serialize(manifest, JsonOptions));
        return (manifest, run);
    }

    public static ExportManifest ParseManifest(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        foreach (var field in new[] { "transactionTime", "request", "requiresAccessToken", "output", "error" })
            if (!root.TryGetProperty(field, out _)) throw new InvalidOperationException($"Manifest missing {field}.");
        var manifest = JsonSerializer.Deserialize<ExportManifest>(json, JsonOptions) ?? throw new InvalidOperationException("Empty manifest.");
        if (manifest.TransactionTime == default || string.IsNullOrWhiteSpace(manifest.Request) || manifest.Output == null || manifest.Error == null
            || manifest.Output.Any(f => f == null || string.IsNullOrEmpty(f.Type) || string.IsNullOrEmpty(f.Url)))
            throw new InvalidOperationException("Invalid export manifest.");
        return manifest;
    }
    public static Uri TrustedUrl(Uri server, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(server, value, out var url)
            || url.Scheme != server.Scheme || url.Authority != server.Authority || url.UserInfo.Length != 0 || url.Fragment.Length != 0)
            throw new InvalidOperationException("Refusing an export URL outside the configured server origin.");
        return url;
    }
    public static TimeSpan RetryDelay(HttpResponseMessage response, int fallback)
    {
        var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds
            ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)?.TotalSeconds ?? fallback;
        return TimeSpan.FromSeconds(Math.Max(seconds, 1));
    }
    private static async Task<Exception> Failure(string step, HttpResponseMessage response, CancellationToken cancellation)
    {
        // OperationOutcome may explain why a request failed, but never print token responses.
        var detail = await response.Content.ReadAsStringAsync(cancellation);
        return new InvalidOperationException($"{step}: HTTP {(int)response.StatusCode}. {detail[..Math.Min(detail.Length, 1000)]}");
    }
}
