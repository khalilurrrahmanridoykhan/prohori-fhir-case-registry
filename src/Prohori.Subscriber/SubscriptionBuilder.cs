using Hl7.Fhir.Model;

namespace Prohori.Subscriber;

/// <summary>
/// Builds the R4 rest-hook <see cref="Subscription"/> this service registers on
/// the FHIR server at startup. Pure function — no I/O — same split as every other
/// builder in this codebase (<c>CaseBundleBuilder</c>, <c>AuditEventBuilder</c>, …).
/// </summary>
public static class SubscriptionBuilder
{
    /// <summary>Matches any newly-created Observation tagged into the shared demo cohort (see Prohori.Api's Systems.ProhoriTag).</summary>
    public const string Criteria = "Observation?_tag=urn:prohori|demo-cohort";

    /// <param name="callbackUrl">
    /// The bare notify URL, no query string — when <c>Payload</c> is set, HAPI's rest-hook
    /// delivery does a <b>PUT</b> to <c>{endpoint}/{ResourceType}/{id}</c> (it treats the
    /// endpoint as a FHIR base URL, not a literal webhook address), simply concatenating
    /// the path onto whatever string is configured — a query string here would end up
    /// AFTER the appended path segments, silently broken. Found by reading HAPI's own
    /// delivery-failure log, not the docs. The shared secret goes in a header instead.
    /// </param>
    public static Subscription Build(string callbackUrl, string notifyKey) => new()
    {
        Status = Subscription.SubscriptionStatus.Requested,
        Reason = "Prohori dashboard live updates (Phase O)",
        Criteria = Criteria,
        Channel = new Subscription.ChannelComponent
        {
            Type = Subscription.SubscriptionChannelType.RestHook,
            Endpoint = callbackUrl,
            Payload = "application/fhir+json",
            Header = [$"X-Notify-Key: {notifyKey}"],
        },
    };
}
