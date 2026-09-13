using Hl7.Fhir.Model;

namespace Prohori.Api.Fhir;

/// <summary>Canonicals for Prohori's one quality measure. See docs/measures.md and the
/// CQL declaration at ig/input/cql/ProhoriDiseasePositivity.cql.</summary>
public static class MeasureCanonicals
{
    public const string MeasureUrl = "https://prohori.health/fhir/Measure/prohori-disease-positivity-measure";
    public const string LibraryUrl = "https://prohori.health/fhir/Library/prohori-disease-positivity";
}

/// <summary>The three CQL-declared populations (initial-population, denominator, numerator),
/// for one group — either the whole cohort or one stratum.</summary>
public readonly record struct PopulationCounts(int InitialPopulation, int Denominator, int Numerator);

/// <summary>
/// Builds a FHIR <see cref="MeasureReport"/> from pre-computed population counts. Pure
/// function — no I/O — same shape as CaseBundleBuilder. The actual population logic (which
/// Encounters/Observations count) lives in <see cref="MeasureEvaluator"/>, which fetches
/// resources and hands the counts here; this only knows how to shape a report.
/// </summary>
public static class MeasureReportBuilder
{
    public static MeasureReport Build(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        PopulationCounts overall,
        IReadOnlyList<(string Stratum, PopulationCounts Counts)> byCity)
    {
        var report = new MeasureReport
        {
            Status = MeasureReport.MeasureReportStatus.Complete,
            Type = MeasureReport.MeasureReportType.Summary,
            Measure = MeasureCanonicals.MeasureUrl,
            Date = DateTimeOffset.UtcNow.ToString("o"),
            Period = new Period { Start = periodStart.ToString("o"), End = periodEnd.ToString("o") },
        };

        var group = new MeasureReport.GroupComponent
        {
            Population = Populations(overall),
        };

        foreach (var (stratum, counts) in byCity)
        {
            group.Stratifier.Add(new MeasureReport.StratifierComponent
            {
                Code = [new CodeableConcept { Text = "city" }],
                Stratum =
                [
                    new MeasureReport.StratifierGroupComponent
                    {
                        Value = new CodeableConcept { Text = stratum },
                        Population = Populations(counts).Select(ToStratifierPopulation).ToList(),
                    },
                ],
            });
        }

        report.Group.Add(group);
        return report;
    }

    private static List<MeasureReport.PopulationComponent> Populations(PopulationCounts counts) =>
    [
        Population("initial-population", counts.InitialPopulation),
        Population("denominator", counts.Denominator),
        Population("numerator", counts.Numerator),
    ];

    private static MeasureReport.PopulationComponent Population(string code, int count) => new()
    {
        Code = new CodeableConcept(Systems.MeasurePopulation, code),
        Count = count,
    };

    private static MeasureReport.StratifierGroupPopulationComponent ToStratifierPopulation(MeasureReport.PopulationComponent p) => new()
    {
        Code = p.Code,
        Count = p.Count,
    };
}
