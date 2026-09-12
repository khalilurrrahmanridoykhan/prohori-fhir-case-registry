using System.ComponentModel.DataAnnotations;

namespace Prohori.Api.Models;

/// <summary>
/// What a legacy ODK/KoBo export hands over: the same case facts <see cref="CaseSubmission"/>
/// takes, except the RDT result is still in the export's plain local code — "pos"/"neg" — rather
/// than SNOMED CT. <see cref="Fhir.LegacyImportEndpoints"/> resolves it via <c>$translate</c>
/// before building the case, so downstream nothing knows the difference.
/// </summary>
public sealed record LegacyCaseSubmission
{
    [Required] public PatientInput Patient { get; init; } = null!;

    [Required] public Disease Disease { get; init; }

    /// <summary>The legacy CodeSystem's code — "pos" or "neg" — not yet SNOMED CT.</summary>
    [Required, RegularExpression("^(pos|neg)$", ErrorMessage = "RdtResultLegacy must be 'pos' or 'neg'.")]
    public string RdtResultLegacy { get; init; } = "";

    [Required] public DateTimeOffset VisitDate { get; init; }
}
