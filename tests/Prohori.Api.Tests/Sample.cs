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

    private static long _sequence;

    /// <summary>A National ID unique to this test run (dodges HAPI-2840 duplicate rejection).
    /// A millisecond timestamp alone collided once two integration test classes started
    /// exercising the shared public sandbox in parallel (xUnit runs different test classes
    /// concurrently by default) — the trailing counter is what actually guarantees uniqueness;
    /// the timestamp just keeps ids roughly sortable. 17 digits, within the 10-17 digit max.</summary>
    public static string FreshNationalId() =>
        "19" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (Interlocked.Increment(ref _sequence) % 100).ToString("D2");
}
