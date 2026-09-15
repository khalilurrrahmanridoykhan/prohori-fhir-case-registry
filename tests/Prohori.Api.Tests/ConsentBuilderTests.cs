using Hl7.Fhir.Model;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

public class ConsentBuilderTests
{
    [Fact]
    public void Build_defaults_to_permit_and_references_the_given_patient()
    {
        var consent = ConsentBuilder.Build("urn:uuid:patient-1", "19942691012345678");

        consent.Status.ShouldBe(Consent.ConsentState.Active);
        consent.Provision.Type.ShouldBe(Consent.ConsentProvisionType.Permit);
        consent.Patient.Reference.ShouldBe("urn:uuid:patient-1");
        consent.Policy.Single().Uri.ShouldBe(ConsentBuilder.PolicyRule);
    }

    [Fact]
    public void Build_carries_its_own_identifier_so_a_return_visit_can_match_it_without_chaining()
    {
        var consent = ConsentBuilder.Build("urn:uuid:patient-1", "19942691012345678");

        var id = consent.Identifier.Single();
        id.System.ShouldBe("http://health.gov.bd/sid");
        id.Value.ShouldBe("19942691012345678");
    }
}
