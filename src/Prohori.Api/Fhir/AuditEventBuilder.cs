using Hl7.Fhir.Model;

namespace Prohori.Api.Fhir;

/// <summary>
/// Builds an <see cref="AuditEvent"/> recording who wrote what, in the same
/// transaction Bundle as the write itself — atomic with the case, not a
/// best-effort side call. Pure function; the agent identity and the list of
/// entities it covers are supplied by the caller. See docs/realtime-provenance-consent.md
/// for why AuditEvent (system-level "who accessed/changed what") was chosen
/// over Provenance (data lineage/derivation) for this.
/// </summary>
public static class AuditEventBuilder
{
    /// <summary>REST create, per the base <c>audit-event-type</c>/<c>audit-event-sub-type</c> ValueSets.</summary>
    private static readonly Coding RestCreate = new("http://terminology.hl7.org/CodeSystem/audit-event-type", "rest")
    {
        Display = "RESTful Operation",
    };
    private static readonly Coding SubTypeCreate = new("http://hl7.org/fhir/restful-interaction", "create")
    {
        Display = "create",
    };

    public static AuditEvent Build(string? agentSubject, IReadOnlyList<(string Urn, string ResourceType)> entities, DateTimeOffset recorded)
    {
        var ev = new AuditEvent
        {
            Type = RestCreate,
            Subtype = [SubTypeCreate],
            Action = AuditEvent.AuditEventAction.C,
            Recorded = recorded.UtcDateTime,
            Outcome = AuditEvent.AuditEventOutcome.N0,
            Agent =
            [
                new AuditEvent.AgentComponent
                {
                    Who = agentSubject is { Length: > 0 }
                        ? new ResourceReference { Display = agentSubject }
                        : new ResourceReference { Display = "unauthenticated" },
                    Requestor = true,
                },
            ],
            Source = new AuditEvent.SourceComponent
            {
                Observer = new ResourceReference { Display = "Prohori.Api" },
            },
        };

        foreach (var (urn, resourceType) in entities)
        {
            ev.Entity.Add(new AuditEvent.EntityComponent
            {
                What = new ResourceReference(urn),
                Type = new Coding("http://terminology.hl7.org/CodeSystem/audit-entity-type", "2") { Display = "System Object" },
                Description = resourceType,
            });
        }

        return ev;
    }

    /// <summary>
    /// A break-glass read: someone with the <c>break-glass</c> scope read a patient
    /// record despite that patient's Consent being set to deny. Recorded with the
    /// standard v3 "BTG" (break the glass) purpose-of-use — a real, named FHIR
    /// mechanism for this, not a bespoke flag — so it shows up distinctly from an
    /// ordinary read in any audit trail built on this data.
    /// </summary>
    public static AuditEvent BuildBreakGlassRead(string? agentSubject, string patientReference, DateTimeOffset recorded)
    {
        var ev = new AuditEvent
        {
            Type = RestCreate,
            Subtype = [new Coding("http://hl7.org/fhir/restful-interaction", "read") { Display = "read" }],
            Action = AuditEvent.AuditEventAction.R,
            Recorded = recorded.UtcDateTime,
            Outcome = AuditEvent.AuditEventOutcome.N0,
            Agent =
            [
                new AuditEvent.AgentComponent
                {
                    Who = new ResourceReference { Display = agentSubject ?? "unauthenticated" },
                    Requestor = true,
                    PurposeOfUse = [new CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-ActReason", "BTG", "break the glass")],
                },
            ],
            Source = new AuditEvent.SourceComponent { Observer = new ResourceReference { Display = "Prohori.Api" } },
            Entity =
            [
                new AuditEvent.EntityComponent
                {
                    What = new ResourceReference(patientReference),
                    Type = new Coding("http://terminology.hl7.org/CodeSystem/audit-entity-type", "1") { Display = "Person" },
                },
            ],
        };
        return ev;
    }
}
