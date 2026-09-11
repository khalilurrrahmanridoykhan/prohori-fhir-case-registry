using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Prohori.Api.Models;

namespace Prohori.Api.Fhir;

/// <summary>Structured Data Capture: serve the field-intake Questionnaire, prefill it for a
/// returning patient ($populate), and turn a filled-in response into a submitted case ($extract).</summary>
public static class QuestionnaireEndpoints
{
    public static void MapQuestionnaire(this WebApplication app)
    {
        app.MapGet("/questionnaire-response/questionnaire", () =>
            Results.Text(QuestionnaireCatalog.Build().ToJson(), "application/fhir+json"))
            .WithSummary("The Prohori field-intake Questionnaire the dashboard's New Case form renders.");

        app.MapPost("/questionnaire-response/$populate", async (PopulateRequest request, FhirClient client) =>
        {
            Patient? patient = null;
            if (!string.IsNullOrWhiteSpace(request.NationalId))
            {
                var bundle = await client.SearchAsync<Patient>(
                    [$"identifier={Systems.NationalId}|{request.NationalId}", "_count=1"]);
                patient = bundle?.Entry.Select(e => e.Resource).OfType<Patient>().FirstOrDefault();
            }
            return Results.Text(QuestionnaireExtraction.Populate(patient).ToJson(), "application/fhir+json");
        })
        .RequireAuthorization("CaseWrite")
        .WithSummary("Prefill the patient-demographics group from an existing Patient, by National ID — a blank response if none matches.");

        app.MapPost("/questionnaire-response/$extract", async (HttpRequest request, FhirCaseService cases, bool dryRun = false) =>
        {
            QuestionnaireResponse response;
            try
            {
                using var reader = new StreamReader(request.Body);
                response = FhirJsonDeserializer.DEFAULT.Deserialize<QuestionnaireResponse>(await reader.ReadToEndAsync());
            }
            // Malformed JSON fails before the FHIR layer even runs; a wrong resourceType or
            // structurally-invalid input fails inside it — both mean the same thing here.
            catch (Exception ex) when (ex is DeserializationFailedException or System.Text.Json.JsonException)
            {
                return Results.BadRequest(new { error = "Invalid QuestionnaireResponse JSON." });
            }

            CaseSubmission submission;
            try { submission = QuestionnaireExtraction.Extract(response); }
            catch (QuestionnaireExtractionException ex) { return Results.ValidationProblem(ex.Errors); }

            var bundle = CaseBundleBuilder.Build(submission);
            if (dryRun) return Results.Text(bundle.ToJson(), "application/fhir+json");

            try
            {
                var result = await cases.SubmitAsync(bundle);
                return Results.Created("/cases", result);
            }
            catch (CaseRejectedException ex)
            {
                return Results.Problem(OperationOutcomeMapper.ToProblemDetails(ex.Outcome, ex.StatusCode));
            }
        })
        .RequireAuthorization("CaseWrite")
        .WithSummary("Extract a filled-in QuestionnaireResponse into a case Bundle (via CaseBundleBuilder) and submit it. ?dryRun=true returns the Bundle without submitting.");
    }
}

public sealed record PopulateRequest(string? NationalId);
