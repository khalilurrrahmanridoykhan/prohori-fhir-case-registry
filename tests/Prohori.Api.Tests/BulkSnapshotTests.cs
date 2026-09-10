using System.Text.Json;
using Prohori.BulkClient;

namespace Prohori.Api.Tests;

public class BulkSnapshotTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "bulk-test-" + Guid.NewGuid());
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-09T00:00:00Z");
    private ExportOptions Options => ExportOptions.Parse(["--output", directory]);
    private async Task<(ExportManifest Manifest, string Run)> Run(params (string Type, string Json)[] files)
    {
        var run = Path.Combine(directory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(run);
        for (var i = 0; i < files.Length; i++) await File.WriteAllTextAsync(Path.Combine(run, $"{i:D4}.ndjson"), files[i].Json + "\n");
        return (new(Start, "http://localhost/$export", true, files.Select(f => new ExportFile(f.Type, "http://localhost/file")).ToArray(), []), run);
    }
    private const string Patient = """{"resourceType":"Patient","id":"p","address":[{"state":"Dhaka"}]}""";
    private const string Visit = """{"resourceType":"Encounter","id":"e","status":"finished","class":{"code":"AMB"},"subject":{"reference":"Patient/p"}}""";
    private static string Observation(string code, string updated = "2026-09-09T00:00:00Z") =>
        $$$"""{"resourceType":"Observation","id":"o","meta":{"lastUpdated":"{{{updated}}}"},"status":"final","code":{"coding":[{"system":"http://loinc.org","code":"42239-4"}]},"subject":{"reference":"Patient/p"},"valueCodeableConcept":{"coding":[{"system":"http://snomed.info/sct","code":"{{{code}}}"}]}}""";

    [Fact]
    public async Task Delta_upserts_preserves_unchanged_resources_and_ignores_older_replays()
    {
        var full = await Run(("Patient", Patient), ("Encounter", Visit), ("Observation", Observation("260385009")));
        (await Snapshot.Apply(Options, full.Manifest, full.Run, default)).ShouldBe(3);
        var delta = await Run(("Observation", Observation("10828004", "2026-09-09T01:00:00Z")));
        var options = Options with { Since = Start };
        await Snapshot.Apply(options, delta.Manifest with { TransactionTime = Start.AddHours(2) }, delta.Run, default);
        // A duplicate older version must not undo the positive correction.
        await Snapshot.Apply(options, full.Manifest with { TransactionTime = Start.AddHours(3) }, full.Run, default);
        using var aggregate = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "aggregate.json")));
        aggregate.RootElement.GetProperty("snapshotResources").GetInt32().ShouldBe(3);
        var row = aggregate.RootElement.GetProperty("divisions")[0];
        row.GetProperty("cases").GetInt32().ShouldBe(1);
        row.GetProperty("positive").GetInt32().ShouldBe(1);
        row.GetProperty("positivityPercent").GetDouble().ShouldBe(100);
    }

    [Fact]
    public async Task Invalid_resource_or_delta_lineage_preserves_checkpoint_and_state()
    {
        var full = await Run(("Patient", Patient));
        await Snapshot.Apply(Options, full.Manifest, full.Run, default);
        var checkpoint = File.ReadAllText(Path.Combine(directory, "checkpoint.json"));
        var state = File.ReadAllText(Path.Combine(directory, "state.ndjson"));
        var wrong = await Run(("Observation", Patient));
        await Should.ThrowAsync<InvalidOperationException>(() => Snapshot.Apply(Options with { Since = Start }, wrong.Manifest, wrong.Run, default));
        foreach (var options in new[] { Options with { Since = Start.AddSeconds(1) }, Options with { Since = Start, Group = "different" } })
            await Should.ThrowAsync<InvalidOperationException>(() => Snapshot.Apply(options, full.Manifest, full.Run, default));
        await Should.ThrowAsync<InvalidOperationException>(() => Snapshot.Apply(Options with { Since = Start }, full.Manifest with { TransactionTime = Start.AddSeconds(-1) }, full.Run, default));
        File.ReadAllText(Path.Combine(directory, "checkpoint.json")).ShouldBe(checkpoint);
        File.ReadAllText(Path.Combine(directory, "state.ndjson")).ShouldBe(state);
    }

    [Fact]
    public async Task Full_refresh_replaces_snapshot_and_unknown_results_have_no_percentage()
    {
        var full = await Run(("Patient", Patient), ("Observation", Observation("unknown")));
        await Snapshot.Apply(Options, full.Manifest, full.Run, default);
        using (var aggregate = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "aggregate.json"))))
            aggregate.RootElement.GetProperty("divisions")[0].GetProperty("positivityPercent").ValueKind.ShouldBe(JsonValueKind.Null);
        var empty = await Run();
        await Snapshot.Apply(Options, empty.Manifest, empty.Run, default);
        File.ReadAllText(Path.Combine(directory, "state.ndjson")).ShouldBeEmpty();
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
