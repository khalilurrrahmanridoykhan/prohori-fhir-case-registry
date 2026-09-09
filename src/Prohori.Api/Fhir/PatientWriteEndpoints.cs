using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Prohori.Api.Fhir;

/// <summary>Version-checked Patient writes. The upstream FHIR server applies patches atomically.</summary>
public static partial class PatientWriteEndpoints
{
    [GeneratedRegex("^[A-Za-z0-9.-]{1,64}$")]
    private static partial Regex FhirId();
    [GeneratedRegex("^W/\"[A-Za-z0-9.-]{1,64}\"$")]
    private static partial Regex VersionTag();

    public static void MapPatientWrites(this WebApplication app)
    {
        app.MapMethods("/cases/{id}", ["PATCH", "PUT"], async (
            string id, HttpRequest request, HttpResponse response, IHttpClientFactory clients, CancellationToken cancellation) =>
        {
            if (!FhirId().IsMatch(id)) return Results.BadRequest(new { error = "Invalid Patient id." });
            var etag = request.Headers.IfMatch.ToString();
            if (string.IsNullOrEmpty(etag)) return Results.Problem(statusCode: 428, detail: "Supply If-Match with the Patient's current weak ETag.");
            if (!VersionTag().IsMatch(etag)) return Results.BadRequest(new { error = "Expected If-Match: W/\"versionId\"." });
            var contentType = request.ContentType?.Split(';')[0].Trim();
            if (contentType != "application/fhir+json" && !(request.Method == "PATCH" && contentType == "application/json-patch+json"))
                return Results.StatusCode(415);
            JsonDocument body;
            try { body = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellation); }
            catch (JsonException) { return Results.BadRequest(new { error = "Invalid JSON." }); }
            using (body)
            {
                var root = body.RootElement;
                if (contentType == "application/fhir+json")
                {
                    var expected = request.Method == "PATCH" ? "Parameters" : "Patient";
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("resourceType", out var type) || type.GetString() != expected)
                        return Results.BadRequest(new { error = $"Expected {expected}." });
                    if (request.Method == "PUT" && (!root.TryGetProperty("id", out var bodyId) || bodyId.GetString() != id))
                        return Results.BadRequest(new { error = "Patient id must match the URL." });
                }
                else if (root.ValueKind != JsonValueKind.Array) return Results.BadRequest(new { error = "JSON Patch must be an array." });
                using var upstream = new HttpRequestMessage(new HttpMethod(request.Method), $"Patient/{id}");
                upstream.Headers.TryAddWithoutValidation("If-Match", etag);
                // Synchronous writes may legally ignore respond-async; never invent a polling URL.
                var preference = request.Headers["Prefer"].ToString().Split(',').Select(x => x.Trim())
                    .FirstOrDefault(x => x is "return=minimal" or "return=representation" or "return=OperationOutcome");
                upstream.Headers.TryAddWithoutValidation("Prefer", preference ?? "return=representation");
                upstream.Content = new StringContent(root.GetRawText(), Encoding.UTF8, contentType);
                try
                {
                    using var result = await clients.CreateClient("fhir").SendAsync(upstream, cancellation);
                    foreach (var name in new[] { "ETag", "Last-Modified", "Preference-Applied" })
                        if (result.Headers.TryGetValues(name, out var values)) response.Headers[name] = values.ToArray();
                    return Results.Text(await result.Content.ReadAsStringAsync(cancellation),
                        result.Content.Headers.ContentType?.ToString() ?? "application/fhir+json", statusCode: (int)result.StatusCode);
                }
                catch (HttpRequestException) { return Results.Problem(statusCode: 502, detail: "FHIR server unavailable."); }
            }
        }).RequireAuthorization("CaseWrite").WithSummary("PATCH or replace the underlying Patient using If-Match optimistic locking.");
    }
}
