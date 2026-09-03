using System.ComponentModel.DataAnnotations;

namespace Prohori.Api.Models;

public enum Disease { Dengue, Malaria }

public enum RdtResult { Positive, Negative }

/// <summary>What a community health worker submits for one field visit.</summary>
public sealed record CaseSubmission
{
    [Required] public PatientInput Patient { get; init; } = null!;

    [Required] public Disease Disease { get; init; }

    [Required] public RdtResult RdtResult { get; init; }

    /// <summary>When the visit happened.</summary>
    [Required] public DateTimeOffset VisitDate { get; init; }
}

public sealed record PatientInput
{
    /// <summary>Bangladesh National ID (NID). 10–17 digits.</summary>
    [Required, RegularExpression(@"^\d{10,17}$", ErrorMessage = "NationalId must be 10–17 digits.")]
    public string NationalId { get; init; } = "";

    [Required, StringLength(100, MinimumLength = 1)]
    public string FamilyName { get; init; } = "";

    public string[] GivenNames { get; init; } = [];

    [Required, RegularExpression("^(male|female|other|unknown)$",
        ErrorMessage = "Gender must be male, female, other or unknown.")]
    public string Gender { get; init; } = "";

    [Required] public DateOnly BirthDate { get; init; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string City { get; init; } = "";

    [Required, StringLength(100, MinimumLength = 1)]
    public string District { get; init; } = "";
}

/// <summary>The resource references the server created, e.g. "Patient/123/_history/1".</summary>
public sealed record CaseResult(IReadOnlyList<string> Created);
