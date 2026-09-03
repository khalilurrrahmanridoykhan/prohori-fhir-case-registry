using System.ComponentModel.DataAnnotations;

namespace Prohori.Api.Models;

/// <summary>
/// A field case to be submitted conformant to BD-Core-FHIR-IG. Carries the extra
/// context BD-Core requires that plain <see cref="CaseSubmission"/> does not:
/// English + Bangla name, division/district/upazila geocodes, the reporting
/// facility, and the practitioner.
/// </summary>
public sealed record BdCoreCaseSubmission
{
    [Required] public BdPatientInput Patient { get; init; } = null!;
    [Required] public Disease Disease { get; init; }
    [Required] public RdtResult RdtResult { get; init; }
    [Required] public DateTimeOffset VisitDate { get; init; }
    [Required] public FacilityInput Facility { get; init; } = null!;

    /// <summary>Practitioner's HRIS code (community health worker / clinician).</summary>
    [Required, StringLength(64, MinimumLength = 1)]
    public string PractitionerCode { get; init; } = "";
}

public sealed record BdPatientInput
{
    [Required, RegularExpression(@"^\d{10,17}$")]
    public string NationalId { get; init; } = "";

    [Required, StringLength(200, MinimumLength = 1)]
    public string NameEnglish { get; init; } = "";

    public string? NameBangla { get; init; }

    [Required, RegularExpression("^(male|female|other|unknown)$")]
    public string Gender { get; init; } = "";

    [Required] public DateOnly BirthDate { get; init; }

    /// <summary>BD geocode for the division, e.g. "30" (Dhaka).</summary>
    [Required, RegularExpression(@"^\d{2,10}$")]
    public string DivisionCode { get; init; } = "";

    /// <summary>BD geocode for the district, e.g. "3026" (Dhaka).</summary>
    [Required, RegularExpression(@"^\d{2,10}$")]
    public string DistrictCode { get; init; } = "";

    /// <summary>BD geocode for the upazila, e.g. "10040028" (Dhamrai).</summary>
    [Required, RegularExpression(@"^\d{2,12}$")]
    public string UpazilaCode { get; init; } = "";
}

public sealed record FacilityInput
{
    /// <summary>HRM facility code, e.g. "10000033".</summary>
    [Required, StringLength(64, MinimumLength = 1)]
    public string Code { get; init; } = "";

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = "";
}
