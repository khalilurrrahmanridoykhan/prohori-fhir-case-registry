using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Prohori.Api.Models;

namespace Prohori.Api.Fhir;

/// <summary>Builds the case Bundle and submits it to the configured FHIR server as one transaction.</summary>
public sealed class FhirCaseService(FhirClient client, ILogger<FhirCaseService> logger)
{
    public Task<CaseResult> SubmitAsync(CaseSubmission submission)
        => SubmitAsync(CaseBundleBuilder.Build(submission));

    /// <summary>Submit a prebuilt transaction Bundle (e.g. the BD-Core variant).</summary>
    public async Task<CaseResult> SubmitAsync(Bundle bundle)
    {
        Bundle? response;
        try
        {
            response = await client.TransactionAsync(bundle);
        }
        catch (FhirOperationException ex)
        {
            logger.LogWarning(ex, "FHIR server rejected the transaction ({Status})", ex.Status);
            var status = (int)ex.Status;
            throw new CaseRejectedException(ex.Outcome, status >= 400 ? status : StatusCodes.Status502BadGateway);
        }

        var created = response?.Entry
            .Select(e => e.Response?.Location)
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Select(location => location!)
            .ToArray() ?? [];

        return new CaseResult(created);
    }
}

/// <summary>Thrown when the FHIR server returns an <see cref="OperationOutcome"/> error.</summary>
public sealed class CaseRejectedException(OperationOutcome? outcome, int statusCode)
    : Exception("The FHIR server rejected the case.")
{
    public OperationOutcome? Outcome { get; } = outcome;
    public int StatusCode { get; } = statusCode;
}
