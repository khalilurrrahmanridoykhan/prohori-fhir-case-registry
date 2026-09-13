using Hl7.Fhir.Model;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

public class MeasureReportBuilderTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(6));
    private static readonly DateTimeOffset End = new(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(6));

    [Fact]
    public void Reports_the_canonical_measure_as_a_complete_summary()
    {
        var report = MeasureReportBuilder.Build(Start, End, new PopulationCounts(8, 8, 4), []);

        report.Status.ShouldBe(MeasureReport.MeasureReportStatus.Complete);
        report.Type.ShouldBe(MeasureReport.MeasureReportType.Summary);
        report.Measure.ShouldBe(MeasureCanonicals.MeasureUrl);
        report.Period.Start.ShouldBe(Start.ToString("o"));
        report.Period.End.ShouldBe(End.ToString("o"));
    }

    [Fact]
    public void The_overall_group_carries_initial_population_denominator_and_numerator()
    {
        var report = MeasureReportBuilder.Build(Start, End, new PopulationCounts(8, 8, 4), []);

        var group = report.Group.ShouldHaveSingleItem();
        CountFor(group.Population, "initial-population").ShouldBe(8);
        CountFor(group.Population, "denominator").ShouldBe(8);
        CountFor(group.Population, "numerator").ShouldBe(4);
    }

    [Fact]
    public void Each_stratum_becomes_its_own_stratifier_group_with_its_own_counts()
    {
        var byCity = new (string, PopulationCounts)[]
        {
            ("Dhaka", new PopulationCounts(3, 3, 2)),
            ("Sylhet", new PopulationCounts(2, 2, 0)),
        };

        var report = MeasureReportBuilder.Build(Start, End, new PopulationCounts(5, 5, 2), byCity);

        var group = report.Group.Single();
        group.Stratifier.Count.ShouldBe(2);

        var dhaka = group.Stratifier[0].Stratum.Single();
        dhaka.Value.Text.ShouldBe("Dhaka");
        CountFor(dhaka.Population, "numerator").ShouldBe(2);

        var sylhet = group.Stratifier[1].Stratum.Single();
        sylhet.Value.Text.ShouldBe("Sylhet");
        CountFor(sylhet.Population, "numerator").ShouldBe(0);
    }

    [Fact]
    public void Population_codes_use_the_standard_measure_population_CodeSystem()
    {
        var report = MeasureReportBuilder.Build(Start, End, new PopulationCounts(1, 1, 1), []);

        var codes = report.Group.Single().Population.Select(p => p.Code.Coding.Single());
        codes.ShouldAllBe(c => c.System == "http://terminology.hl7.org/CodeSystem/measure-population");
        codes.Select(c => c.Code).ShouldBe(["initial-population", "denominator", "numerator"]);
    }

    private static int? CountFor(IEnumerable<MeasureReport.PopulationComponent> populations, string code) =>
        populations.Single(p => p.Code.Coding.Single().Code == code).Count;

    private static int? CountFor(IEnumerable<MeasureReport.StratifierGroupPopulationComponent> populations, string code) =>
        populations.Single(p => p.Code.Coding.Single().Code == code).Count;
}
