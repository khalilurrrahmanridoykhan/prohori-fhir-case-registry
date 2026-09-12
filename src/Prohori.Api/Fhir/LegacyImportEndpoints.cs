using Hl7.Fhir.Serialization;
using MiniValidation;
using Prohori.Api.Models;

namespace Prohori.Api.Fhir;

/// <summary>Submit a case from a legacy ODK/KoBo export — same Bundle, a third way in.</summary>
public static class LegacyImportEndpoints
{
    public static void MapLegacyImport(this WebApplication app)
    {
        app.MapPost("/legacy-import/cases", async (LegacyCaseSubmission submission, TerminologyClient terminology, FhirCaseService cases, bool dryRun = false) =>
        {
            if (!MiniValidator.TryValidate(submission, out var errors))
                return Results.ValidationProblem(errors);

            var translation = await terminology.TranslateRdtResultAsync(submission.RdtResultLegacy);
            if (!translation.Success || translation.Target is not { } target)
                return Results.Problem(statusCode: 502, detail:
                    $"Could not translate legacy RDT code '{submission.RdtResultLegacy}' to SNOMED CT. " +
                    "Is prohori-rdt-result-legacy-to-snomed loaded on this server? See scripts/load-terminology.sh.");

            RdtResult? rdtResult = target.Code switch
            {
                "10828004" => RdtResult.Positive,
                "260385009" => RdtResult.Negative,
                _ => null,
            };
            if (rdtResult is null)
                return Results.Problem(statusCode: 502, detail: $"$translate returned an unexpected SNOMED code: {target.Code}.");

            var caseSubmission = new CaseSubmission
            {
                Patient = submission.Patient,
                Disease = submission.Disease,
                RdtResult = rdtResult.Value,
                VisitDate = submission.VisitDate,
            };

            if (dryRun)
                return Results.Text(CaseBundleBuilder.Build(caseSubmission).ToJson(), "application/fhir+json");

            try
            {
                var result = await cases.SubmitAsync(caseSubmission);
                return Results.Created("/cases", result);
            }
            catch (CaseRejectedException ex)
            {
                return Results.Problem(OperationOutcomeMapper.ToProblemDetails(ex.Outcome, ex.StatusCode));
            }
        })
        .RequireAuthorization("CaseWrite")
        .WithSummary("Submit a case from a legacy ODK export (plain 'pos'/'neg' RDT codes). " +
            "$translate resolves the code to SNOMED CT, then the case goes through the same " +
            "CaseBundleBuilder / FhirCaseService the typed API and the SDC form both use. " +
            "?dryRun=true returns the Bundle without submitting.");
    }
}
