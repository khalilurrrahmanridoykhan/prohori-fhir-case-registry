using Hl7.Fhir.Model;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

public class CaseBundleBuilderTests
{
    [Fact]
    public void Bundle_is_a_transaction()
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case());
        bundle.Type.ShouldBe(Bundle.BundleType.Transaction);
    }

    [Fact]
    public void Positive_case_has_patient_encounter_observation_and_condition()
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case(result: RdtResult.Positive));

        bundle.Entry.Select(e => e.Resource.TypeName)
            .ShouldBe(["Patient", "Encounter", "Observation", "Condition"]);
    }

    [Fact]
    public void Negative_case_has_no_condition()
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case(result: RdtResult.Negative));

        bundle.Entry.Select(e => e.Resource.TypeName)
            .ShouldBe(["Patient", "Encounter", "Observation"]);
    }

    [Fact]
    public void Every_entry_posts_by_a_relative_type_url()
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case());

        foreach (var entry in bundle.Entry)
        {
            entry.Request.Method.ShouldBe(Bundle.HTTPVerb.POST);
            entry.Request.Url.ShouldBeOneOf("Patient", "Encounter", "Observation", "Condition");
            entry.FullUrl.ShouldStartWith("urn:uuid:");
        }
    }

    [Fact]
    public void Patient_entry_carries_a_conditional_create_on_the_national_id()
    {
        var submission = Sample.Case(nationalId: "12345678901");
        var bundle = CaseBundleBuilder.Build(submission);

        var patientEntry = bundle.Entry.Single(e => e.Resource is Patient);
        patientEntry.Request.IfNoneExist
            .ShouldBe("identifier=http://health.gov.bd/sid|12345678901");
    }

    [Fact]
    public void References_use_the_patient_and_encounter_fullUrls()
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case(result: RdtResult.Positive));

        var patientUrn = bundle.Entry.Single(e => e.Resource is Patient).FullUrl;
        var encounterUrn = bundle.Entry.Single(e => e.Resource is Encounter).FullUrl;
        var observation = (Observation)bundle.Entry.Single(e => e.Resource is Observation).Resource;
        var condition = (Condition)bundle.Entry.Single(e => e.Resource is Condition).Resource;

        observation.Subject.Reference.ShouldBe(patientUrn);
        observation.Encounter.Reference.ShouldBe(encounterUrn);
        condition.Subject.Reference.ShouldBe(patientUrn);
        condition.Encounter.Reference.ShouldBe(encounterUrn);
    }

    [Theory]
    [InlineData(Disease.Dengue, "42239-4")]
    [InlineData(Disease.Malaria, "70048-1")]
    public void Observation_uses_the_right_LOINC_code_for_the_disease(Disease disease, string expectedLoinc)
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case(disease));
        var observation = (Observation)bundle.Entry.Single(e => e.Resource is Observation).Resource;

        var coding = ((CodeableConcept)observation.Code).Coding.Single();
        coding.System.ShouldBe("http://loinc.org");
        coding.Code.ShouldBe(expectedLoinc);
    }

    [Theory]
    [InlineData(RdtResult.Positive, "10828004")]
    [InlineData(RdtResult.Negative, "260385009")]
    public void Observation_value_maps_the_rdt_result_to_SNOMED(RdtResult result, string expectedSnomed)
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case(result: result));
        var observation = (Observation)bundle.Entry.Single(e => e.Resource is Observation).Resource;

        var coding = ((CodeableConcept)observation.Value).Coding.Single();
        coding.System.ShouldBe("http://snomed.info/sct");
        coding.Code.ShouldBe(expectedSnomed);
    }

    [Fact]
    public void Dengue_condition_is_coded_with_both_SNOMED_and_ICD10()
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case(Disease.Dengue, RdtResult.Positive));
        var condition = (Condition)bundle.Entry.Single(e => e.Resource is Condition).Resource;

        var systems = condition.Code.Coding.Select(c => (c.System, c.Code)).ToArray();
        systems.ShouldContain(("http://snomed.info/sct", "38362002"));
        systems.ShouldContain(("http://hl7.org/fhir/sid/icd-10", "A90"));
    }

    [Fact]
    public void Every_resource_is_tagged_for_the_demo_cohort()
    {
        var bundle = CaseBundleBuilder.Build(Sample.Case(result: RdtResult.Positive));

        foreach (var entry in bundle.Entry)
        {
            entry.Resource.Meta.Tag
                .ShouldContain(t => t.System == "urn:prohori" && t.Code == "demo-cohort");
        }
    }
}
