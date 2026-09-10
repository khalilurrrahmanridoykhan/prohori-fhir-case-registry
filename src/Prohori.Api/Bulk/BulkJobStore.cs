namespace Prohori.Api.Bulk;

/// <summary>Bounded, single-instance lab job registry. Losing process state fails closed.</summary>
public sealed class BulkJobStore
{
    private readonly Dictionary<string, BulkJob> _jobs = new();
    private readonly object _gate = new();
    public BulkJob? Add(string owner, Uri statusUrl, string requestUrl)
    {
        lock (_gate)
        {
            foreach (var expired in _jobs.Where(x => x.Value.Expires <= DateTimeOffset.UtcNow).Select(x => x.Key).ToArray()) _jobs.Remove(expired);
            if (_jobs.Count >= 100) return null;
            var job = new BulkJob(Guid.NewGuid().ToString("N"), owner, statusUrl, requestUrl, DateTimeOffset.UtcNow.AddHours(24));
            _jobs.Add(job.Id, job);
            return job;
        }
    }
    public BulkJob? Find(string id, string owner)
    {
        lock (_gate)
            return _jobs.TryGetValue(id, out var job) && job.Owner == owner && job.Expires > DateTimeOffset.UtcNow ? job : null;
    }
}

public sealed record BulkJob(string Id, string Owner, Uri StatusUrl, string RequestUrl, DateTimeOffset Expires)
{
    public IReadOnlyDictionary<string, Uri> Files { get; set; } = new Dictionary<string, Uri>();
}
