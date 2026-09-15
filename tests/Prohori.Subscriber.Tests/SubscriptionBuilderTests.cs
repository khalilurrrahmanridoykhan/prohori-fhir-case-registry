using Hl7.Fhir.Model;
using Prohori.Subscriber;

namespace Prohori.Subscriber.Tests;

public class SubscriptionBuilderTests
{
    [Fact]
    public void Build_is_a_rest_hook_subscription_requesting_activation()
    {
        var sub = SubscriptionBuilder.Build("http://host.docker.internal:5300/notify", "abc");

        sub.Status.ShouldBe(Subscription.SubscriptionStatus.Requested);
        sub.Channel.Type.ShouldBe(Subscription.SubscriptionChannelType.RestHook);
        sub.Channel.Endpoint.ShouldBe("http://host.docker.internal:5300/notify");
        sub.Criteria.ShouldBe(SubscriptionBuilder.Criteria);
    }

    [Fact]
    public void Build_carries_the_shared_secret_as_a_header_not_a_query_string()
    {
        // A query string on the endpoint breaks once HAPI appends /{ResourceType}/{id}
        // after it (see SubscriptionBuilder's own doc comment) — the secret has to
        // travel as a header instead.
        var sub = SubscriptionBuilder.Build("http://host.docker.internal:5300/notify", "abc");

        sub.Channel.Header.ShouldContain("X-Notify-Key: abc");
    }
}
