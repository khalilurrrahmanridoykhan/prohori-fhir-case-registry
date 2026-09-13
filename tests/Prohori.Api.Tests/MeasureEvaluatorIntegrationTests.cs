using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging.Abstractions;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

/// <summary>
/// Hits a real FHIR server, same convention as CaseSubmissionIntegrationTests. Submits fresh
/// cases for today, then evaluates the measure over a window that must contain them — asserting
/// "at least" rather than an exact count, since the shared public sandbox may carry other
/// same-day demo-cohort data from other runs.
/// </summary>
[Trait("Category", "Integration")]
public class MeasureEvaluatorIntegrationTests
{
    private static (FhirClient Client, FhirCaseService Cases, MeasureEvaluator Evaluator) NewClients()
    {
        var baseUrl = Environment.GetEnvironmentVariable("PROHORI_FHIR_BASEURL") ?? "https://hapi.fhir.org/baseR4";
        var client = new FhirClient(baseUrl, new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json,
            VerifyFhirVersion = false,
        });
        return (client, new FhirCaseService(client, NullLogger<FhirCaseService>.Instance), new MeasureEvaluator(client));
    }

    [Fact]
    public async Task Evaluating_today_counts_at_least_the_cases_just_submitted()
    {
        var (_, cases, evaluator) = NewClients();
        var today = DateTimeOffset.UtcNow;

        await cases.SubmitAsync(Sample.Case(Disease.Dengue, RdtResult.Positive, Sample.FreshNationalId()) with { VisitDate = today });
        await cases.SubmitAsync(Sample.Case(Disease.Malaria, RdtResult.Negative, Sample.FreshNationalId()) with { VisitDate = today });

        var report = await evaluator.EvaluateAsync(today.AddDays(-1), today.AddDays(1));

        var group = report.Group.Single();
        int? Count(string code) => group.Population.Single(p => p.Code.Coding.Single().Code == code).Count;

        Count("denominator")!.Value.ShouldBeGreaterThanOrEqualTo(2);
        Count("numerator")!.Value.ShouldBeGreaterThanOrEqualTo(1);
        Count("initial-population").ShouldBe(Count("denominator")); // this measure has no exclusions
    }

    [Fact]
    public async Task A_period_that_excludes_the_visit_reports_a_smaller_denominator()
    {
        var (_, cases, evaluator) = NewClients();
        var today = DateTimeOffset.UtcNow;
        var farPast = today.AddYears(-5);

        await cases.SubmitAsync(Sample.Case(nationalId: Sample.FreshNationalId()) with { VisitDate = today });

        var includingToday = await evaluator.EvaluateAsync(today.AddDays(-1), today.AddDays(1));
        var excludingToday = await evaluator.EvaluateAsync(farPast.AddDays(-1), farPast.AddDays(1));

        int Denominator(Hl7.Fhir.Model.MeasureReport r) =>
            r.Group.Single().Population.Single(p => p.Code.Coding.Single().Code == "denominator").Count ?? 0;

        Denominator(includingToday).ShouldBeGreaterThanOrEqualTo(1);
        Denominator(excludingToday).ShouldBe(0);
    }
}
