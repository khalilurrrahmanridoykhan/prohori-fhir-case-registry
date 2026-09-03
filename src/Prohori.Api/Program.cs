using System.Text.Json;
using System.Text.Json.Serialization;
using Hl7.Fhir.Rest;
using MiniValidation;
using Prohori.Api.Fhir;
using Prohori.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

// FHIR server: config key "Fhir:BaseUrl" or env var Fhir__BaseUrl; defaults to the public HAPI sandbox.
var fhirBaseUrl = builder.Configuration["Fhir:BaseUrl"] ?? "https://hapi.fhir.org/baseR4";

builder.Services.AddSingleton(_ => new FhirClient(fhirBaseUrl, new FhirClientSettings
{
    PreferredFormat = ResourceFormat.Json,
    VerifyFhirVersion = false,
    PreferredParameterHandling = SearchParameterHandling.Lenient,
}));
builder.Services.AddScoped<FhirCaseService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapGet("/health", () => Results.Ok(new { status = "ok", fhirBaseUrl }))
   .WithSummary("Liveness + the FHIR server this instance targets.");

app.MapPost("/cases", async (CaseSubmission submission, FhirCaseService cases) =>
{
    if (!MiniValidator.TryValidate(submission, out var errors))
        return Results.ValidationProblem(errors);

    try
    {
        var result = await cases.SubmitAsync(submission);
        return Results.Created("/cases", result);
    }
    catch (CaseRejectedException ex)
    {
        return Results.Problem(OperationOutcomeMapper.ToProblemDetails(ex.Outcome, ex.StatusCode));
    }
})
.WithSummary("Submit one field case — builds a Patient/Encounter/Observation(/Condition) transaction Bundle and posts it to the FHIR server.");

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the API in tests.</summary>
public partial class Program;
