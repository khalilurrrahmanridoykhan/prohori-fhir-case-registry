using System.Text.Json;

namespace Prohori.Subscriber;

/// <summary>
/// Turns the raw JSON body HAPI posts to <c>/notify</c> into the small payload
/// broadcast to dashboard tabs. Pure function — no I/O — so it is unit-testable
/// directly, same split as every other parser in this codebase (<c>TerminologyClient.ParseTranslateResponse</c>).
/// Parses defensively: R4 rest-hook delivery is a real FHIR mechanism, but exactly
/// what lands in the body (the resource itself vs. an empty ping) is a server
/// choice, not something this service controls — see docs/realtime-provenance-consent.md.
/// </summary>
public static class NotificationParser
{
    public static string ToDashboardPayload(string rawBody)
    {
        string? resourceType = null;
        string? id = null;
        string? subjectReference = null;

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("resourceType", out var rt)) resourceType = rt.GetString();
            if (root.TryGetProperty("id", out var idEl)) id = idEl.GetString();
            if (root.TryGetProperty("subject", out var subj) && subj.TryGetProperty("reference", out var r))
                subjectReference = r.GetString();
        }
        catch (JsonException)
        {
            // Empty-payload rest-hook ping, or something we don't recognize — still a
            // real event worth nudging the dashboard about, just with less detail.
        }

        return JsonSerializer.Serialize(new
        {
            resourceType = resourceType ?? "unknown",
            id,
            subject = subjectReference,
            receivedAt = DateTimeOffset.UtcNow,
        });
    }
}
