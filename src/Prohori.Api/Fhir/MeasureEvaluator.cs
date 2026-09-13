using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace Prohori.Api.Fhir;

/// <summary>
/// Evaluates Prohori's one quality measure — "field visits in a period" (denominator) and
/// "of those, how many were RDT-positive" (numerator) — against the configured FHIR server,
/// for a given period, stratified by city.
/// <para/>
/// This computes the same populations <c>ig/input/cql/ProhoriDiseasePositivity.cql</c>
/// declares, by hand, in C# — not by interpreting the CQL. Local HAPI has no CQL engine wired
/// up (checked: <c>Measure/$evaluate-measure</c> returns "does not know how to handle [this]
/// operation" even with the clinical-reasoning module explicitly enabled — see
/// docs/measures.md). The CQL is the reviewable declaration of the populations; this is the
/// executable side, the same relationship every other phase's declared-but-not-executed
/// artifact has had to its C# implementation.
/// </summary>
public sealed class MeasureEvaluator(FhirClient client)
{
    public async Task<MeasureReport> EvaluateAsync(DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken cancellation = default)
    {
        var searchParams = new SearchParams()
            .Where($"date=ge{periodStart:yyyy-MM-dd}")
            .Where($"date=le{periodEnd:yyyy-MM-dd}")
            .Where($"_tag={Systems.ProhoriTag}|{Systems.DemoCohortCode}")
            .Include("Encounter:subject")
            .ReverseInclude("Observation:encounter")
            .LimitTo(300);

        var bundle = await client.SearchAsync<Encounter>(searchParams, ct: cancellation);
        var resources = (bundle?.Entry ?? []).Select(e => e.Resource).Where(r => r != null).Select(r => r!).ToList();

        var patients = resources.OfType<Patient>().Where(p => p.Id != null).ToDictionary(p => p.Id!);
        var encounters = resources.OfType<Encounter>().Where(e => e.Id != null).ToList();
        var observationsByEncounter = resources.OfType<Observation>()
            .Where(o => o.Encounter?.Reference != null)
            .ToLookup(o => ReferenceId(o.Encounter!.Reference));

        PopulationCounts CountsFor(IEnumerable<Encounter> group)
        {
            var list = group.ToList();
            var numerator = list.Count(e => IsPositive(e, observationsByEncounter));
            return new PopulationCounts(list.Count, list.Count, numerator);
        }

        var overall = CountsFor(encounters);

        var byCity = encounters
            .GroupBy(e => CityFor(e, patients))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (Stratum: g.Key, Counts: CountsFor(g)))
            .ToList();

        return MeasureReportBuilder.Build(periodStart, periodEnd, overall, byCity);
    }

    private static bool IsPositive(Encounter encounter, ILookup<string?, Observation> observationsByEncounter) =>
        observationsByEncounter[encounter.Id]
            .Any(o => (o.Value as CodeableConcept)?.Coding.Any(c => c.System == Systems.Snomed && c.Code == "10828004") == true);

    private static string CityFor(Encounter encounter, IReadOnlyDictionary<string, Patient> patients)
    {
        var patientId = ReferenceId(encounter.Subject?.Reference);
        var patient = patientId != null && patients.TryGetValue(patientId, out var p) ? p : null;
        return patient?.Address.FirstOrDefault()?.City ?? "Unknown";
    }

    /// <summary>"Patient/123" or "Patient/123/_history/1" -> "123".</summary>
    private static string? ReferenceId(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return null;
        var withoutHistory = reference.Split("/_history/")[0];
        var slash = withoutHistory.LastIndexOf('/');
        return slash >= 0 ? withoutHistory[(slash + 1)..] : withoutHistory;
    }
}
