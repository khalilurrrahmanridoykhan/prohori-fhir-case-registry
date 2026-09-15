using System.Text.Json;
using Prohori.Subscriber;

namespace Prohori.Subscriber.Tests;

public class NotificationParserTests
{
    [Fact]
    public void Observation_notification_carries_id_and_subject()
    {
        var body = """{"resourceType":"Observation","id":"obs-1","subject":{"reference":"Patient/pat-1"}}""";

        var payload = JsonDocument.Parse(NotificationParser.ToDashboardPayload(body)).RootElement;

        payload.GetProperty("resourceType").GetString().ShouldBe("Observation");
        payload.GetProperty("id").GetString().ShouldBe("obs-1");
        payload.GetProperty("subject").GetString().ShouldBe("Patient/pat-1");
    }

    [Fact]
    public void Empty_body_still_produces_a_valid_payload()
    {
        var payload = JsonDocument.Parse(NotificationParser.ToDashboardPayload("")).RootElement;

        payload.GetProperty("resourceType").GetString().ShouldBe("unknown");
    }

    [Fact]
    public void Malformed_json_does_not_throw()
    {
        var payload = JsonDocument.Parse(NotificationParser.ToDashboardPayload("not json")).RootElement;

        payload.GetProperty("resourceType").GetString().ShouldBe("unknown");
    }
}
