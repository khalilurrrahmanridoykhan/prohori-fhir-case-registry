using Prohori.Api.Bulk;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using MiniValidation;
using Prohori.Api.Fhir;
using Prohori.Api.Models;

// Offline artifact generation keeps CI independent of an identity provider.
if (args.Length == 3 && args[0] == "--export-bd-core")
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    var submission = JsonSerializer.Deserialize<BdCoreCaseSubmission>(File.ReadAllText(args[1]), options)
        ?? throw new ArgumentException("Missing submission.");
    if (!MiniValidator.TryValidate(submission, out _)) throw new ArgumentException("Invalid submission.");
    File.WriteAllText(args[2], BdCoreBundleBuilder.Build(submission).ToJson());
    return;
}
if (args.Length == 2 && args[0] == "--export-questionnaire")
{
    File.WriteAllText(args[1], QuestionnaireCatalog.Build().ToJson());
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

// FHIR server: config key "Fhir:BaseUrl" or env var Fhir__BaseUrl; defaults to the public HAPI sandbox.
var fhirBaseUrl = builder.Configuration["Fhir:BaseUrl"] ?? "https://hapi.fhir.org/baseR4";

builder.Services.AddScoped(_ => new FhirClient(fhirBaseUrl, new FhirClientSettings
{
    PreferredFormat = ResourceFormat.Json,
    VerifyFhirVersion = false,
    PreferredParameterHandling = SearchParameterHandling.Lenient,
}));
builder.Services.AddScoped<FhirCaseService>();
builder.Services.AddScoped<TerminologyClient>();
builder.Services.AddScoped<MeasureEvaluator>();
builder.Services.AddHttpClient("fhir", client => client.BaseAddress = new Uri(fhirBaseUrl.TrimEnd('/') + "/"))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Auth:Authority"] ?? (builder.Environment.IsDevelopment()
        ? "http://localhost:8081/realms/prohori" : "https://localhost:8443/realms/prohori");
    options.Audience = "prohori-api";
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.MapInboundClaims = false;
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BulkRead", policy => policy.RequireAuthenticatedUser().RequireClaim("sub")
        .RequireAssertion(context => context.User.FindAll("scope").SelectMany(c => c.Value.Split(' ')).Contains("system/*.read", StringComparer.Ordinal)));
    options.AddPolicy("PatientRead", policy => policy.RequireAuthenticatedUser().RequireClaim("patient")
        .RequireAssertion(context => context.User.FindAll("scope").SelectMany(c => c.Value.Split(' ')).Contains("patient/*.rs")));
    options.AddPolicy("CaseWrite", policy =>
    policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.FindAll("scope")
        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Contains("user/*.write", StringComparer.Ordinal)));
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration["Smart:WebOrigin"] ?? "http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("ETag", "Preference-Applied")));
builder.Services.AddSingleton<BulkJobStore>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

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
.RequireAuthorization("CaseWrite")
.WithSummary("Submit one field case — builds a Patient/Encounter/Observation(/Condition) transaction Bundle and posts it to the FHIR server.");

app.MapPost("/bd-core/cases", async (BdCoreCaseSubmission submission, FhirCaseService cases, bool dryRun = false) =>
{
    if (!MiniValidator.TryValidate(submission, out var errors))
        return Results.ValidationProblem(errors);

    var bundle = BdCoreBundleBuilder.Build(submission);

    if (dryRun)
        return Results.Text(bundle.ToJson(), "application/fhir+json");

    try
    {
        var result = await cases.SubmitAsync(bundle);
        return Results.Created("/bd-core/cases", result);
    }
    catch (CaseRejectedException ex)
    {
        return Results.Problem(OperationOutcomeMapper.ToProblemDetails(ex.Outcome, ex.StatusCode));
    }
})
.RequireAuthorization("CaseWrite")
.WithSummary("Submit one field case as a BD-Core-FHIR-IG conformant Bundle (Organization/Practitioner/Patient/Encounter/Observation/Condition). ?dryRun=true returns the Bundle without submitting.");

app.MapPatientWrites();
app.MapLocalSmart();
app.MapBulk();
app.MapQuestionnaire();
app.MapLegacyImport();
app.MapMeasure();

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the API in tests.</summary>
public partial class Program;
