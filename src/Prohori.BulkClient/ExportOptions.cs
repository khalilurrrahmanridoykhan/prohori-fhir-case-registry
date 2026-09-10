using System.Globalization;

namespace Prohori.BulkClient;

public sealed record ExportOptions(Uri BaseUrl, Uri TokenEndpoint, string ClientId, string KeyPath, string Output,
    string Mode, string Group, string Types, DateTimeOffset? Since, string[] TypeFilters, int TimeoutSeconds, int PollSeconds)
{
    public static ExportOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>();
        var filters = new List<string>();
        var known = new HashSet<string> { "--base-url", "--token-endpoint", "--client-id", "--key", "--output", "--mode", "--group", "--type", "--since", "--type-filter", "--timeout-seconds", "--poll-seconds" };
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!known.Contains(args[i]) || i + 1 >= args.Length) throw new ArgumentException($"Unknown or missing option: {args[i]}");
            if (args[i] == "--type-filter") filters.Add(args[i + 1]);
            else if (!values.TryAdd(args[i], args[i + 1])) throw new ArgumentException($"Repeated option: {args[i]}");
        }
        string Get(string name, string fallback) => values.GetValueOrDefault(name, fallback);
        var mode = Get("--mode", "group");
        if (mode is not ("group" or "patient" or "system")) throw new ArgumentException("--mode must be group, patient or system.");
        DateTimeOffset? since = null;
        if (values.TryGetValue("--since", out var value))
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                || !(value.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(value, "[+-][0-9]{2}:[0-9]{2}$")))
                throw new ArgumentException("--since requires an ISO timestamp with a timezone.");
            since = parsed;
        }
        var timeout = int.Parse(Get("--timeout-seconds", "600"), CultureInfo.InvariantCulture);
        var poll = int.Parse(Get("--poll-seconds", "2"), CultureInfo.InvariantCulture);
        if (timeout < 1 || poll < 1 || poll > 60) throw new ArgumentException("Timeout must be positive; polling must be 1–60 seconds.");
        return new ExportOptions(Endpoint(Get("--base-url", "http://localhost:5280/bulk/fhir/")),
            Endpoint(Get("--token-endpoint", "http://localhost:8091/realms/prohori-bulk/protocol/openid-connect/token")),
            Get("--client-id", "prohori-bulk-client"), Get("--key", ".bulk/client-key.pem"), Get("--output", ".bulk/export"),
            mode, Get("--group", "prohori-cohort"), Get("--type", "Patient,Encounter,Observation,Condition"), since,
            filters.ToArray(), timeout, poll);
    }
    public static Uri Endpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.Query.Length != 0)
            throw new ArgumentException("Endpoints must use HTTPS (HTTP is allowed only on loopback) without credentials, query or fragment.");
        return uri;
    }
    public Uri Kickoff()
    {
        var path = Mode switch { "system" => "$export", "patient" => "Patient/$export", _ => $"Group/{Uri.EscapeDataString(Group)}/$export" };
        var query = new List<KeyValuePair<string, string>> { new("_type", Types), new("_outputFormat", "application/fhir+ndjson") };
        if (Since.HasValue) query.Add(new("_since", Since.Value.ToString("O", CultureInfo.InvariantCulture)));
        query.AddRange(TypeFilters.Select(filter => new KeyValuePair<string, string>("_typeFilter", filter)));
        return new Uri(BaseUrl.AbsoluteUri.TrimEnd('/') + "/" + path + "?" + string.Join('&', query.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value))));
    }
}
