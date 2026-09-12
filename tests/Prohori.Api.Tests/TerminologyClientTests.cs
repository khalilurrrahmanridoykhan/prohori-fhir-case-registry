using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

/// <summary>
/// Pure parsing of a $translate <c>Parameters</c> response — no HTTP, no server. The fixtures
/// mirror what a real local HAPI actually returned when this was verified against
/// prohori-rdt-result-legacy-to-snomed (see scripts/verify-terminology.sh).
/// </summary>
public class TerminologyClientTests
{
    private const string PositiveMatch = """
        {
          "resourceType": "Parameters",
          "parameter": [
            { "name": "result", "valueBoolean": true },
            { "name": "message", "valueString": "Matches found" },
            { "name": "match", "part": [
              { "name": "equivalence", "valueCode": "equivalent" },
              { "name": "concept", "valueCoding": { "system": "http://snomed.info/sct", "code": "10828004", "display": "Positive" } },
              { "name": "source", "valueUri": "https://prohori.health/fhir/ConceptMap/prohori-rdt-result-legacy-to-snomed" }
            ] }
          ]
        }
        """;

    private const string NoMatch = """
        {
          "resourceType": "Parameters",
          "parameter": [
            { "name": "result", "valueBoolean": false },
            { "name": "message", "valueString": "No matches found" }
          ]
        }
        """;

    [Fact]
    public void A_matched_translation_reports_success_and_the_target_coding()
    {
        var result = TerminologyClient.ParseTranslateResponse(PositiveMatch);

        result.Success.ShouldBeTrue();
        result.Target.ShouldNotBeNull();
        result.Target!.System.ShouldBe("http://snomed.info/sct");
        result.Target.Code.ShouldBe("10828004");
    }

    [Fact]
    public void No_match_reports_failure_with_no_target()
    {
        var result = TerminologyClient.ParseTranslateResponse(NoMatch);

        result.Success.ShouldBeFalse();
        result.Target.ShouldBeNull();
    }

    [Fact]
    public void Malformed_JSON_reports_failure_rather_than_throwing()
    {
        var result = TerminologyClient.ParseTranslateResponse("{ not json");

        result.Success.ShouldBeFalse();
        result.Target.ShouldBeNull();
    }

    [Fact]
    public void A_result_of_true_with_no_match_part_is_not_treated_as_success()
    {
        // Defensive: never hand back Success=true without a concept to act on.
        const string inconsistent = """{ "resourceType": "Parameters", "parameter": [{ "name": "result", "valueBoolean": true }] }""";

        var result = TerminologyClient.ParseTranslateResponse(inconsistent);

        result.Success.ShouldBeFalse();
        result.Target.ShouldBeNull();
    }
}
