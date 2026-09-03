using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;

namespace Prohori.Api.Fhir;

/// <summary>
/// Maps a FHIR <see cref="OperationOutcome"/> (the universal FHIR error payload)
/// onto an RFC 7807 <see cref="ProblemDetails"/> so API callers get one
/// predictable error shape.
/// </summary>
public static class OperationOutcomeMapper
{
    public static ProblemDetails ToProblemDetails(OperationOutcome? outcome, int status)
    {
        var issues = outcome?.Issue ?? [];

        var errorText = issues
            .Where(IsError)
            .Select(Describe)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        var problem = new ProblemDetails
        {
            Title = "The FHIR server rejected the case",
            Status = status,
            Detail = errorText.Length > 0
                ? string.Join("; ", errorText)
                : "The server returned no error detail.",
        };

        problem.Extensions["issues"] = issues.Select(i => new
        {
            severity = i.Severity?.ToString().ToLowerInvariant(),
            code = i.Code?.ToString(),
            diagnostics = i.Diagnostics,
            expression = i.Expression?.ToArray(),
        }).ToArray();

        return problem;
    }

    private static bool IsError(OperationOutcome.IssueComponent i)
        => i.Severity is OperationOutcome.IssueSeverity.Error or OperationOutcome.IssueSeverity.Fatal;

    private static string Describe(OperationOutcome.IssueComponent i)
        => i.Diagnostics
           ?? i.Details?.Text
           ?? i.Code?.ToString()
           ?? "unknown error";
}
