using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Prohori.V2Gateway;

var builder = WebApplication.CreateBuilder(args);

// Same convention as Prohori.Api: Fhir:BaseUrl / Fhir__BaseUrl, defaults to the public sandbox.
var fhirBaseUrl = builder.Configuration["Fhir:BaseUrl"] ?? "https://hapi.fhir.org/baseR4";

builder.Services.AddScoped(_ => new FhirClient(fhirBaseUrl, new FhirClientSettings
{
    PreferredFormat = ResourceFormat.Json,
    VerifyFhirVersion = false,
}));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", fhirBaseUrl }));

app.MapPost("/adt", async (HttpRequest request, FhirClient client, bool dryRun = false) =>
{
    using var reader = new StreamReader(request.Body);
    var er7 = await reader.ReadToEndAsync();

    AdtMessage message;
    try
    {
        message = V2Parser.Parse(er7);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        return Results.BadRequest(new { error = $"Could not parse the ADT message: {ex.Message}" });
    }

    var bundle = V2ToFhirMapper.Build(message);
    if (dryRun) return Results.Text(bundle.ToJson(), "application/fhir+json");

    Hl7.Fhir.Model.Bundle? response;
    try
    {
        response = await client.TransactionAsync(bundle);
    }
    catch (FhirOperationException ex)
    {
        var status = (int)ex.Status;
        return Results.Problem(statusCode: status >= 400 ? status : StatusCodes.Status502BadGateway,
            detail: ex.Outcome?.ToString() ?? ex.Message);
    }

    var created = response?.Entry
        .Select(e => e.Response?.Location)
        .Where(location => !string.IsNullOrWhiteSpace(location))
        .ToArray() ?? [];
    return Results.Created("/adt", new { triggerEvent = message.TriggerEvent, mrn = message.Mrn, created });
})
.WithSummary("Accept a raw ER7 (pipe-delimited) ADT^A01/A08/A03 message, map it to a Patient/Encounter transaction Bundle, and submit it. ?dryRun=true returns the Bundle without submitting.")
.Accepts<string>("text/plain");

app.Run();

/// <summary>Exposed so WebApplicationFactory&lt;Program&gt; can host this in tests.</summary>
public partial class Program;
