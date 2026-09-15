using Hl7.Fhir.Model;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

public class AuditEventBuilderTests
{
    [Fact]
    public void Build_records_a_create_action_with_one_entity_per_write()
    {
        var entities = new List<(string Urn, string ResourceType)>
        {
            ("urn:uuid:patient", "Patient"),
            ("urn:uuid:encounter", "Encounter"),
        };

        var ev = AuditEventBuilder.Build("test-user", entities, DateTimeOffset.UtcNow);

        ev.Action.ShouldBe(AuditEvent.AuditEventAction.C);
        ev.Entity.Count.ShouldBe(2);
        ev.Entity.Select(e => e.What.Reference).ShouldBe(["urn:uuid:patient", "urn:uuid:encounter"]);
    }

    [Fact]
    public void Build_records_the_agent_subject_as_the_who_display()
    {
        var ev = AuditEventBuilder.Build("keycloak-subject-123", [], DateTimeOffset.UtcNow);

        ev.Agent.Single().Who.Display.ShouldBe("keycloak-subject-123");
        ev.Agent.Single().Requestor.ShouldBe(true);
    }

    [Fact]
    public void Build_falls_back_to_unauthenticated_when_no_agent_is_known()
    {
        var ev = AuditEventBuilder.Build(null, [], DateTimeOffset.UtcNow);

        ev.Agent.Single().Who.Display.ShouldBe("unauthenticated");
    }

    [Fact]
    public void BuildBreakGlassRead_uses_the_v3_break_the_glass_purpose_of_use()
    {
        var ev = AuditEventBuilder.BuildBreakGlassRead("test-user", "Patient/123", DateTimeOffset.UtcNow);

        ev.Action.ShouldBe(AuditEvent.AuditEventAction.R);
        var purpose = ev.Agent.Single().PurposeOfUse.Single().Coding.Single();
        purpose.System.ShouldBe("http://terminology.hl7.org/CodeSystem/v3-ActReason");
        purpose.Code.ShouldBe("BTG");
        ev.Entity.Single().What.Reference.ShouldBe("Patient/123");
    }
}
