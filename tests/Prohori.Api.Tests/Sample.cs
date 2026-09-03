namespace Prohori.Api.Tests;

/// <summary>Builds valid <see cref="CaseSubmission"/> instances for tests.</summary>
public static class Sample
{
    public static CaseSubmission Case(
        Disease disease = Disease.Dengue,
        RdtResult result = RdtResult.Positive,
        string? nationalId = null) => new()
    {
        Patient = new PatientInput
        {
            NationalId = nationalId ?? "19942691012345678",
            FamilyName = "Khan",
            GivenNames = ["Rahman"],
            Gender = "male",
            BirthDate = new DateOnly(1995, 6, 15),
            City = "Dhaka",
            District = "Dhaka",
        },
        Disease = disease,
        RdtResult = result,
        VisitDate = new DateTimeOffset(2026, 8, 14, 9, 20, 0, TimeSpan.FromHours(6)),
    };

    /// <summary>A National ID unique to this test run (dodges HAPI-2840 duplicate rejection).</summary>
    public static string FreshNationalId() =>
        "19" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // 15 digits
}
