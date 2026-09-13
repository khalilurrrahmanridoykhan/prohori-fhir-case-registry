using Hl7.Fhir.Serialization;

namespace Prohori.Api.Fhir;

/// <summary>Evaluate Prohori's one quality measure over a period — real numbers, not the
/// dashboard's client-side arithmetic. See docs/measures.md.</summary>
public static class MeasureEndpoints
{
    public static void MapMeasure(this WebApplication app)
    {
        app.MapGet("/measure/$evaluate-measure", async (DateTimeOffset periodStart, DateTimeOffset periodEnd, MeasureEvaluator evaluator) =>
        {
            if (periodEnd < periodStart)
                return Results.BadRequest(new { error = "periodEnd must not be before periodStart." });

            var report = await evaluator.EvaluateAsync(periodStart, periodEnd);
            return Results.Text(report.ToJson(), "application/fhir+json");
        })
        .WithSummary("Evaluate the field-visits / RDT-positivity measure over [periodStart, periodEnd], stratified by city. " +
            "Public, like the dashboard's own FHIR reads — no case data is written or exposed beyond what /Encounter search already returns.");
    }
}
