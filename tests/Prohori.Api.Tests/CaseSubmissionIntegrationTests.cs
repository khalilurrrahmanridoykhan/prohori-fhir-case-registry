using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging.Abstractions;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

/// <summary>
/// Hits a real FHIR server. Excluded from the CI gate
/// (<c>dotnet test --filter Category!=Integration</c>); run locally with:
/// <c>dotnet test --filter Category=Integration</c>.
/// Override the target with <c>PROHORI_FHIR_BASEURL</c>.
/// </summary>
[Trait("Category", "Integration")]
public class CaseSubmissionIntegrationTests
{
    private static FhirCaseService NewService()
    {
        var baseUrl = Environment.GetEnvironmentVariable("PROHORI_FHIR_BASEURL")
                      ?? "https://hapi.fhir.org/baseR4";
        var client = new FhirClient(baseUrl, new FhirClientSettings
        {
            PreferredFormat = ResourceFormat.Json,
            VerifyFhirVersion = false,
        });
        return new FhirCaseService(client, NullLogger<FhirCaseService>.Instance);
    }

    [Fact]
    public async Task Submitting_a_positive_case_creates_the_case_plus_consent_and_an_audit_event()
    {
        var service = NewService();

        var result = await service.SubmitAsync(
            Sample.Case(Disease.Dengue, RdtResult.Positive, Sample.FreshNationalId()), agent: "test-agent");

        result.Created.ShouldContain(l => l.StartsWith("Patient/"));
        result.Created.ShouldContain(l => l.StartsWith("Encounter/"));
        result.Created.ShouldContain(l => l.StartsWith("Observation/"));
        result.Created.ShouldContain(l => l.StartsWith("Condition/"));
        // Phase O: every write also gets a default-permit Consent for the patient
        // and an AuditEvent recording who wrote it — same transaction, same atomicity.
        result.Created.ShouldContain(l => l.StartsWith("Consent/"));
        result.Created.ShouldContain(l => l.StartsWith("AuditEvent/"));
    }

    [Fact]
    public async Task A_second_visit_for_the_same_patient_reuses_the_existing_patient()
    {
        var service = NewService();
        var nid = Sample.FreshNationalId();

        var firstVisit = Sample.Case(nationalId: nid);
        var secondVisit = firstVisit with { VisitDate = firstVisit.VisitDate.AddDays(7) };

        var first = await service.SubmitAsync(firstVisit);
        var second = await service.SubmitAsync(secondVisit);

        static string PatientId(Prohori.Api.Models.CaseResult r) =>
            r.Created.Single(l => l.StartsWith("Patient/")).Split('/')[1];

        // If-None-Exist on the National ID matched the existing patient — no duplicate.
        PatientId(second).ShouldBe(PatientId(first));
    }

    [Fact]
    public async Task A_second_visit_for_the_same_patient_reuses_the_existing_consent()
    {
        var service = NewService();
        var nid = Sample.FreshNationalId();

        var firstVisit = Sample.Case(nationalId: nid);
        var secondVisit = firstVisit with { VisitDate = firstVisit.VisitDate.AddDays(7) };

        var first = await service.SubmitAsync(firstVisit);
        var second = await service.SubmitAsync(secondVisit);

        static string ConsentId(Prohori.Api.Models.CaseResult r) =>
            r.Created.Single(l => l.StartsWith("Consent/")).Split('/')[1];

        // The Consent's own conditional create (patient:identifier=...) matched the one
        // made on the first visit — same idempotency guarantee as the Patient itself.
        ConsentId(second).ShouldBe(ConsentId(first));
    }
}
