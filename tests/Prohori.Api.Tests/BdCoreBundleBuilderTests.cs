using Hl7.Fhir.Model;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

public class BdCoreBundleBuilderTests
{
    private static BdCoreCaseSubmission Sample(RdtResult result = RdtResult.Positive, Disease disease = Disease.Dengue) => new()
    {
        Patient = new BdPatientInput
        {
            NationalId = "19942691012345678",
            NameEnglish = "Rahman Khan",
            NameBangla = "রহমান খান",
            Gender = "male",
            BirthDate = new DateOnly(1995, 6, 15),
            DivisionCode = "30",
            DistrictCode = "3026",
            UpazilaCode = "10040028",
        },
        Disease = disease,
        RdtResult = result,
        VisitDate = new DateTimeOffset(2026, 8, 14, 9, 20, 0, TimeSpan.FromHours(6)),
        Facility = new FacilityInput { Code = "10000033", Name = "Dhaka Medical College Hospital" },
        PractitionerCode = "CHW-2201",
    };

    [Fact]
    public void Bundle_is_org_practitioner_patient_encounter_observation()
    {
        var bundle = BdCoreBundleBuilder.Build(Sample(RdtResult.Positive));

        bundle.Type.ShouldBe(Bundle.BundleType.Transaction);
        bundle.Entry.Select(e => e.Resource.TypeName)
            .ShouldBe(["Organization", "Practitioner", "Patient", "Encounter", "Observation"]);
    }

    [Fact]
    public void No_separate_condition_resource_is_emitted()
    {
        // bd-condition v0.4.6 is unsubmittable (empty ICD-11 ValueSet) — the
        // diagnosis rides on Encounter.reasonCode instead.
        BdCoreBundleBuilder.Build(Sample(RdtResult.Positive))
            .Entry.Any(e => e.Resource is Condition).ShouldBeFalse();
    }

    [Fact]
    public void Every_resource_declares_its_bd_core_profile()
    {
        var bundle = BdCoreBundleBuilder.Build(Sample());

        foreach (var entry in bundle.Entry)
        {
            var profile = entry.Resource.Meta?.Profile?.FirstOrDefault();
            profile.ShouldNotBeNull();
            profile.ShouldStartWith("https://fhir.dghs.gov.bd/core/StructureDefinition/");
        }
    }

    [Fact]
    public void Patient_carries_the_UHID_and_NID_identifiers()
    {
        var patient = (Patient)Built().Entry.Single(e => e.Resource is Patient).Resource;

        patient.Identifier.Select(i => i.System)
            .ShouldBe(["http://dghs.gov.bd/identifier/uhid", "http://dghs.gov.bd/identifier/nid"], ignoreOrder: true);
        patient.Identifier.Single(i => i.System.EndsWith("/nid"))
            .Type.Coding[0].Code.ShouldBe("NID");
    }

    [Fact]
    public void Patient_name_text_has_english_and_bangla_extensions()
    {
        var patient = (Patient)Built().Entry.Single(e => e.Resource is Patient).Resource;
        var urls = patient.Name[0].TextElement.Extension.Select(e => e.Url).ToArray();

        urls.ShouldContain("https://fhir.dghs.gov.bd/core/StructureDefinition/bd-name-en");
        urls.ShouldContain("https://fhir.dghs.gov.bd/core/StructureDefinition/bd-name-bn");
    }

    [Fact]
    public void Patient_address_has_division_and_upazila_geocode_extensions()
    {
        var patient = (Patient)Built().Entry.Single(e => e.Resource is Patient).Resource;
        var address = patient.Address[0];

        var division = address.GetExtension("https://fhir.dghs.gov.bd/core/StructureDefinition/bd-divisions");
        var upazila = address.GetExtension("https://fhir.dghs.gov.bd/core/StructureDefinition/bd-upazillas");

        ((CodeableConcept)division.Value).Coding[0].System.ShouldBe("https://fhir.dghs.gov.bd/core/CodeSystem/bd-geocodes");
        ((CodeableConcept)division.Value).Coding[0].Code.ShouldBe("30");
        ((CodeableConcept)upazila.Value).Coding[0].Code.ShouldBe("10040028");
        address.District.ShouldBe("3026");
        address.Country.ShouldBe("BD");
    }

    [Fact]
    public void Encounter_links_practitioner_and_service_provider()
    {
        var bundle = Built();
        var orgUrn = bundle.Entry.Single(e => e.Resource is Organization).FullUrl;
        var practitionerUrn = bundle.Entry.Single(e => e.Resource is Practitioner).FullUrl;
        var encounter = (Encounter)bundle.Entry.Single(e => e.Resource is Encounter).Resource;

        encounter.ServiceProvider.Reference.ShouldBe(orgUrn);
        encounter.Participant[0].Individual.Reference.ShouldBe(practitionerUrn);
        encounter.Participant[0].Period.Start.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(Disease.Dengue, "1D40")]
    [InlineData(Disease.Malaria, "1F4Z")]
    public void Positive_case_records_the_icd11_diagnosis_on_the_encounter(Disease disease, string expectedIcd11)
    {
        var bundle = BdCoreBundleBuilder.Build(Sample(RdtResult.Positive, disease));
        var encounter = (Encounter)bundle.Entry.Single(e => e.Resource is Encounter).Resource;

        var reason = encounter.ReasonCode.Single().Coding.Single();
        reason.System.ShouldBe("http://id.who.int/icd/release/11/mms");
        reason.Code.ShouldBe(expectedIcd11);
    }

    [Fact]
    public void Negative_case_records_no_diagnosis()
    {
        var encounter = (Encounter)BdCoreBundleBuilder.Build(Sample(RdtResult.Negative))
            .Entry.Single(e => e.Resource is Encounter).Resource;

        encounter.ReasonCode.ShouldBeEmpty();
    }

    [Fact]
    public void Observation_subject_has_a_display_and_encounter_reference()
    {
        var observation = (Observation)Built().Entry.Single(e => e.Resource is Observation).Resource;

        observation.Subject.Display.ShouldBe("Rahman Khan");
        observation.Encounter.Reference.ShouldStartWith("urn:uuid:");
        observation.Performer.ShouldHaveSingleItem();
    }

    private static Bundle Built() => BdCoreBundleBuilder.Build(Sample());
}
