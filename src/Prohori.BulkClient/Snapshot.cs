using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace Prohori.BulkClient;

/// <summary>Small-cohort snapshot and aggregate. Deltas upsert resources; a full export replaces the snapshot.</summary>
public static class Snapshot
{
    public static async System.Threading.Tasks.Task<int> Apply(ExportOptions options, ExportManifest manifest, string run, CancellationToken cancellation)
    {
        var statePath = Path.Combine(options.Output, "state.ndjson");
        var checkpointPath = Path.Combine(options.Output, "checkpoint.json");
        var cohort = JsonSerializer.Serialize(new { server = options.BaseUrl.AbsoluteUri.TrimEnd('/'), options.Mode, options.Group, options.Types, options.TypeFilters });
        var resources = new Dictionary<string, (Resource Resource, string Json)>();
        if (options.Since.HasValue)
        {
            if (!File.Exists(checkpointPath)) throw new InvalidOperationException("An incremental export requires an existing full snapshot in the same output directory.");
            using var checkpoint = JsonDocument.Parse(await File.ReadAllTextAsync(checkpointPath, cancellation));
            if (checkpoint.RootElement.GetProperty("cohort").GetString() != cohort) throw new InvalidOperationException("Export scope differs from the stored snapshot.");
            var watermark = checkpoint.RootElement.GetProperty("transactionTime").GetDateTimeOffset();
            if (manifest.TransactionTime < watermark) throw new InvalidOperationException("Export watermark predates the stored snapshot.");
            if (options.Since > watermark) throw new InvalidOperationException("_since is newer than the stored watermark; this would leave a gap.");
            await ReadFile(statePath, null, resources, cancellation);
        }
        var downloaded = 0;
        for (var i = 0; i < manifest.Output.Length; i++)
            downloaded += await ReadFile(Path.Combine(run, $"{i:D4}.ndjson"), manifest.Output[i].Type, resources, cancellation);
        var aggregate = Aggregate(resources.Values.Select(x => x.Resource), downloaded, manifest.TransactionTime);
        // Complete parsing and aggregation before replacing any previously usable snapshot.
        var stateTemp = Path.Combine(run, "state.ndjson");
        await File.WriteAllLinesAsync(stateTemp, resources.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value.Json), cancellation);
        var aggregateTemp = Path.Combine(run, "aggregate.json");
        await File.WriteAllTextAsync(aggregateTemp, JsonSerializer.Serialize(aggregate, BulkExport.JsonOptions), cancellation);
        var checkpointTemp = Path.Combine(run, "checkpoint.json");
        await File.WriteAllTextAsync(checkpointTemp, JsonSerializer.Serialize(new { cohort, transactionTime = manifest.TransactionTime }, BulkExport.JsonOptions), cancellation);
        File.Copy(stateTemp, statePath + ".tmp", true);
        File.Move(statePath + ".tmp", statePath, true);
        File.Copy(aggregateTemp, Path.Combine(options.Output, "aggregate.json"), true);
        // Advance watermark last; a crash before this point safely replays an overlapping delta.
        File.Copy(checkpointTemp, checkpointPath + ".tmp", true);
        File.Move(checkpointPath + ".tmp", checkpointPath, true);
        return downloaded;
    }

    private static async System.Threading.Tasks.Task<int> ReadFile(string path, string? expectedType,
        Dictionary<string, (Resource Resource, string Json)> resources, CancellationToken cancellation)
    {
        var parser = new FhirJsonDeserializer();
        var count = 0;
        using var reader = new StreamReader(path);
        while (await reader.ReadLineAsync(cancellation) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var resource = parser.Deserialize<Resource>(line);
            if (resource is not (Patient or Encounter or Observation or Condition) || string.IsNullOrWhiteSpace(resource.Id)
                || (expectedType != null && resource.TypeName != expectedType))
                throw new InvalidOperationException($"Invalid or mismatched FHIR resource in {Path.GetFileName(path)} line {count + 1}.");
            var key = resource.TypeName + "/" + resource.Id;
            // Overlapping delta replays must never replace a newer local version with an older one.
            if (!resources.TryGetValue(key, out var old) || old.Resource.Meta?.LastUpdated == null
                || resource.Meta?.LastUpdated >= old.Resource.Meta.LastUpdated)
                resources[key] = (resource, line);
            count++;
        }
        return count;
    }

    public static object Aggregate(IEnumerable<Resource> input, int downloaded, DateTimeOffset transactionTime)
    {
        var resources = input.ToArray();
        var patients = resources.OfType<Patient>().ToDictionary(p => "Patient/" + p.Id, StringComparer.Ordinal);
        var observations = resources.OfType<Observation>().Where(o => o.Code?.Coding.Any(c =>
            c.System == "http://loinc.org" && c.Code is "42239-4" or "70569-9") == true).ToArray();
        var rows = observations.Select(o =>
        {
            var patient = o.Subject?.Reference != null ? patients.GetValueOrDefault(o.Subject.Reference) : null;
            var result = (o.Value as CodeableConcept)?.Coding.FirstOrDefault(c => c.System == "http://snomed.info/sct")?.Code;
            return new { division = patient?.Address.FirstOrDefault()?.State ?? "Unknown", result };
        }).ToArray();
        var visits = resources.OfType<Encounter>().Select(e =>
            e.Subject?.Reference is { } reference ? patients.GetValueOrDefault(reference)?.Address.FirstOrDefault()?.State ?? "Unknown" : "Unknown").ToArray();
        var divisions = rows.Select(r => r.division).Concat(visits).Distinct().OrderBy(x => x).Select(division =>
        {
            var group = rows.Where(r => r.division == division).ToArray();
            var positive = group.Count(x => x.result == "10828004");
            var negative = group.Count(x => x.result == "260385009");
            return new { division, cases = visits.Count(v => v == division), rdtObservations = group.Count(), positive, negative,
                unknown = group.Count() - positive - negative, positivityPercent = positive + negative == 0 ? (double?)null : Math.Round(100.0 * positive / (positive + negative), 2) };
        }).ToArray();
        return new { transactionTime, downloadedResources = downloaded, snapshotResources = resources.Length,
            patients = patients.Count, encounters = resources.OfType<Encounter>().Count(),
            rdtObservations = observations.Length, divisions };
    }
}
