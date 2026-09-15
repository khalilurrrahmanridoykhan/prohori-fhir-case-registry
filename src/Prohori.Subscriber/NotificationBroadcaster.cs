using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Prohori.Subscriber;

/// <summary>
/// In-memory fan-out from the one rest-hook <c>/notify</c> callback HAPI calls
/// to every dashboard tab currently holding an open <c>/stream</c> (SSE)
/// connection. One process, one notification stream — no message broker
/// needed at this scale, and the notification is a live nudge to re-fetch,
/// not the record of truth (that's the FHIR server itself).
/// </summary>
public sealed class NotificationBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    /// <summary>The most recent payload broadcast — lets a verification script poll
    /// without having to hold its own SSE connection open.</summary>
    public string? LastPayload { get; private set; }

    public Guid Subscribe(out ChannelReader<string> reader)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        _subscribers[id] = channel;
        reader = channel.Reader;
        return id;
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
            channel.Writer.TryComplete();
    }

    public void Publish(string payload)
    {
        LastPayload = payload;
        foreach (var channel in _subscribers.Values)
            channel.Writer.TryWrite(payload);
    }
}
