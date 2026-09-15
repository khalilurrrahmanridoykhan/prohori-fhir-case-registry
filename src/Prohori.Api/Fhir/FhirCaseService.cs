using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Prohori.Api.Models;

namespace Prohori.Api.Fhir;

/// <summary>Builds the case Bundle and submits it to the configured FHIR server as one transaction.</summary>
public sealed class FhirCaseService(FhirClient client, ILogger<FhirCaseService> logger)
{
    public Task<CaseResult> SubmitAsync(CaseSubmission submission, string? agent = null)
        => SubmitAsync(CaseBundleBuilder.Build(submission), agent);

    /// <summary>
    /// Submit a prebuilt transaction Bundle (e.g. the BD-Core or legacy-import variant).
    /// Every path into this method — the single choke point every case write funnels
    /// through, same as Phase K/L/M's "one shared Bundle path" — gets the same two
    /// additions appended atomically, in the same transaction as the case itself:
    /// a default-permit <see cref="Consent"/> for any newly-created Patient (conditional
    /// create, so a second visit for the same patient doesn't duplicate it), and an
    /// <see cref="AuditEvent"/> naming every resource the transaction wrote and who wrote it.
    /// </summary>
    public async Task<CaseResult> SubmitAsync(Bundle bundle, string? agent = null)
    {
        var augmented = Augment(bundle, agent);

        Bundle? response;
        try
        {
            response = await client.TransactionAsync(augmented);
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

    private static Bundle Augment(Bundle bundle, string? agent)
    {
        var writes = bundle.Entry
            .Where(e => e.Request?.Method == Bundle.HTTPVerb.POST && e.Resource is not null)
            .ToList();

        var entities = writes.Select(e => (Urn: e.FullUrl!, ResourceType: e.Resource!.TypeName)).ToList();

        foreach (var patientEntry in writes.Where(e => e.Resource is Patient))
        {
            var patient = (Patient)patientEntry.Resource!;
            var nid = patient.Identifier.FirstOrDefault(i => i.System == Systems.NationalId)?.Value;
            if (nid is null) continue; // nothing to key a Consent's own identifier on

            var consent = ConsentBuilder.Build(patientEntry.FullUrl!, nid);
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                FullUrl = "urn:uuid:" + Guid.NewGuid(),
                Resource = consent,
                Request = new Bundle.RequestComponent
                {
                    Method = Bundle.HTTPVerb.POST,
                    Url = "Consent",
                    // A plain identifier= match on Consent's own identifier — not chained through
                    // the patient reference (patient.identifier=... is also valid FHIR, but breaks
                    // inside a transaction Bundle here; see ConsentBuilder's doc comment). Don't
                    // create a second Consent for a returning patient's next visit.
                    IfNoneExist = $"identifier={Systems.NationalId}|{nid}",
                },
            });
        }

        var audit = AuditEventBuilder.Build(agent, entities, DateTimeOffset.UtcNow);
        bundle.Entry.Add(new Bundle.EntryComponent
        {
            FullUrl = "urn:uuid:" + Guid.NewGuid(),
            Resource = audit,
            Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.POST, Url = "AuditEvent" },
        });

        return bundle;
    }
}

/// <summary>Thrown when the FHIR server returns an <see cref="OperationOutcome"/> error.</summary>
public sealed class CaseRejectedException(OperationOutcome? outcome, int statusCode)
    : Exception("The FHIR server rejected the case.")
{
    public OperationOutcome? Outcome { get; } = outcome;
    public int StatusCode { get; } = statusCode;
}
