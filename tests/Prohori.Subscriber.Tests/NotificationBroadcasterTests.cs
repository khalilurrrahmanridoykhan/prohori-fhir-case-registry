using Prohori.Subscriber;

namespace Prohori.Subscriber.Tests;

public class NotificationBroadcasterTests
{
    [Fact]
    public async Task A_published_payload_reaches_a_subscribed_reader()
    {
        var broadcaster = new NotificationBroadcaster();
        var id = broadcaster.Subscribe(out var reader);

        broadcaster.Publish("""{"resourceType":"Observation"}""");

        var received = await reader.ReadAsync();
        received.ShouldBe("""{"resourceType":"Observation"}""");
        broadcaster.Unsubscribe(id);
    }

    [Fact]
    public void Publish_records_the_last_payload_for_out_of_band_polling()
    {
        var broadcaster = new NotificationBroadcaster();

        broadcaster.Publish("""{"resourceType":"Observation","id":"1"}""");

        broadcaster.LastPayload.ShouldBe("""{"resourceType":"Observation","id":"1"}""");
    }

    [Fact]
    public void An_unsubscribed_reader_gets_nothing_further()
    {
        var broadcaster = new NotificationBroadcaster();
        var id = broadcaster.Subscribe(out var reader);
        broadcaster.Unsubscribe(id);

        broadcaster.Publish("""{"resourceType":"Observation"}""");

        reader.Completion.IsCompleted.ShouldBeTrue();
    }
}
