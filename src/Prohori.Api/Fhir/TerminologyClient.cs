using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace Prohori.Api.Fhir;

/// <summary>What a ConceptMap $translate call resolved a source code to, if anything.</summary>
public sealed record TranslationResult(bool Success, Coding? Target);

/// <summary>
/// Calls the configured FHIR server's terminology operations. Thin on purpose — the interesting
/// work (the actual ValueSet/CodeSystem/ConceptMap content) lives in <c>ig/input/fsh/ProhoriTerminology.fsh</c>
/// and is loaded onto the server by <c>scripts/load-terminology.sh</c>; this class just calls
/// <c>$translate</c> against whatever server is configured. See docs/terminology.md for why
/// that has to be a live server rather than the static validator.
/// </summary>
public sealed class TerminologyClient(IHttpClientFactory clients)
{
    private const string ConceptMapUrl = "https://prohori.health/fhir/ConceptMap/prohori-rdt-result-legacy-to-snomed";
    private const string LegacySystem = "https://prohori.health/fhir/CodeSystem/prohori-rdt-result-legacy";

    /// <summary>Translate a legacy "pos"/"neg" RDT code to its SNOMED CT equivalent.</summary>
    public async Task<TranslationResult> TranslateRdtResultAsync(string legacyCode, CancellationToken cancellation = default)
    {
        var url = "ConceptMap/$translate"
            + $"?url={Uri.EscapeDataString(ConceptMapUrl)}"
            + $"&system={Uri.EscapeDataString(LegacySystem)}"
            + $"&code={Uri.EscapeDataString(legacyCode)}";

        using var http = clients.CreateClient("fhir");
        using var response = await http.GetAsync(url, cancellation);
        if (!response.IsSuccessStatusCode) return new TranslationResult(false, null);

        return ParseTranslateResponse(await response.Content.ReadAsStringAsync(cancellation));
    }

    /// <summary>Pure parsing of a $translate response's <c>result</c> and first <c>match.concept</c>. No I/O — unit-testable directly.</summary>
    public static TranslationResult ParseTranslateResponse(string json)
    {
        Parameters parameters;
        try
        {
            parameters = FhirJsonDeserializer.DEFAULT.Deserialize<Parameters>(json);
        }
        catch (Exception ex) when (ex is DeserializationFailedException or System.Text.Json.JsonException)
        {
            return new TranslationResult(false, null);
        }

        var success = (parameters.Parameter.Find(p => p.Name == "result")?.Value as FhirBoolean)?.Value ?? false;
        var concept = parameters.Parameter.Find(p => p.Name == "match")?.Part.Find(p => p.Name == "concept")?.Value as Coding;
        return new TranslationResult(success && concept != null, concept);
    }
}
