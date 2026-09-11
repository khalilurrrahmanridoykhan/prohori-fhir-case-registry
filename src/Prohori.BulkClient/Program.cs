using System.Security.Cryptography;
using Prohori.BulkClient;

try
{
    if (args is ["--keygen", var directory])
    {
        BackendKey.Generate(directory);
        Console.WriteLine($"Generated private key (local only) and public JWKS in {directory}.");
        return;
    }
    if (args is ["--help"])
    {
        Console.WriteLine("Prohori.BulkClient: authenticated FHIR Bulk Data export (run from repository root).");
        Console.WriteLine("--keygen DIRECTORY | --base-url URL --token-endpoint URL --client-id ID --key PEM --output DIRECTORY");
        Console.WriteLine("--mode group|patient|system --group ID --type Patient,Encounter,Observation,Condition");
        Console.WriteLine("--since ISO_TIMESTAMP --type-filter FHIR_SEARCH (repeatable) --timeout-seconds 600 --poll-seconds 2");
        return;
    }
    var options = ExportOptions.Parse(args);
    using var key = RSA.Create();
    key.ImportFromPem(File.ReadAllText(options.KeyPath));
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(90) };
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; timeout.Cancel(); };
    Directory.CreateDirectory(options.Output);
    // Concurrent clients cannot corrupt a shared snapshot/checkpoint.
    using var outputLock = new FileStream(Path.Combine(options.Output, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    var authentication = new BackendAuthentication(http, options.ClientId, options.TokenEndpoint, key);
    var (manifest, run) = await new BulkExport(http, authentication, options).Run(timeout.Token);
    var count = await Snapshot.Apply(options, manifest, run, timeout.Token);
    Console.WriteLine($"Downloaded {count} resources. Aggregate: {Path.Combine(options.Output, "aggregate.json")}");
    Console.WriteLine($"Next _since watermark: {manifest.TransactionTime:O}");
}
catch (OperationCanceledException) { Console.Error.WriteLine("Export cancelled or exceeded its time limit; checkpoint not advanced."); Environment.ExitCode = 1; }
catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException or FormatException or HttpRequestException or CryptographicException)
{ Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
