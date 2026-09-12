using Hl7.Fhir.Model;
using Prohori.V2Gateway;

namespace Prohori.V2Gateway.Tests;

public class V2ToFhirMapperTests
{
    private static AdtMessage Admit(string trigger = "A01", string mrn = "MRN100234", string? visitNumber = "VN5001") => new()
    {
        TriggerEvent = trigger,
        Mrn = mrn,
        FamilyName = "Khan",
        GivenNames = ["Rahman"],
        Gender = "M",
        BirthDate = "19950615",
        City = "Dhaka",
        District = "Dhaka",
        VisitNumber = visitNumber,
        EncounterClass = "I",
        AdmitDateTime = new DateTimeOffset(2026, 8, 14, 9, 0, 0, TimeSpan.FromHours(6)),
    };

    [Fact]
    public void An_admit_conditionally_creates_the_Patient_and_Encounter()
    {
        var bundle = V2ToFhirMapper.Build(Admit("A01"));

        bundle.Type.ShouldBe(Bundle.BundleType.Transaction);
        bundle.Entry.Select(e => e.Resource.TypeName).ShouldBe(["Patient", "Encounter"]);

        var patientEntry = bundle.Entry.Single(e => e.Resource is Patient);
        patientEntry.Request.Method.ShouldBe(Bundle.HTTPVerb.POST);
        patientEntry.Request.Url.ShouldBe("Patient");
        patientEntry.Request.IfNoneExist.ShouldBe($"identifier={V2Systems.Mrn}|MRN100234");

        var encounterEntry = bundle.Entry.Single(e => e.Resource is Encounter);
        encounterEntry.Request.Method.ShouldBe(Bundle.HTTPVerb.POST);
        encounterEntry.Request.Url.ShouldBe("Encounter");
        encounterEntry.Request.IfNoneExist.ShouldBe($"identifier={V2Systems.VisitNumber}|VN5001");
    }

    [Fact]
    public void An_update_conditionally_PUTs_by_the_v2_identifiers_instead_of_creating()
    {
        var bundle = V2ToFhirMapper.Build(Admit("A08"));

        var patientEntry = bundle.Entry.Single(e => e.Resource is Patient);
        patientEntry.Request.Method.ShouldBe(Bundle.HTTPVerb.PUT);
        patientEntry.Request.Url.ShouldBe($"Patient?identifier={V2Systems.Mrn}|MRN100234");
        patientEntry.Request.IfNoneExist.ShouldBeNull();

        var encounterEntry = bundle.Entry.Single(e => e.Resource is Encounter);
        encounterEntry.Request.Method.ShouldBe(Bundle.HTTPVerb.PUT);
        encounterEntry.Request.Url.ShouldBe($"Encounter?identifier={V2Systems.VisitNumber}|VN5001");
    }

    [Fact]
    public void Encounter_and_Patient_reference_each_other_by_the_bundle_internal_urn()
    {
        var bundle = V2ToFhirMapper.Build(Admit());

        var patientUrn = bundle.Entry.Single(e => e.Resource is Patient).FullUrl;
        var encounter = (Encounter)bundle.Entry.Single(e => e.Resource is Encounter).Resource;

        encounter.Subject.Reference.ShouldBe(patientUrn);
    }

    [Fact]
    public void A_discharge_finishes_the_existing_Encounter_rather_than_creating_a_new_one()
    {
        var discharge = Admit("A03") with { DischargeDateTime = new DateTimeOffset(2026, 8, 16, 11, 0, 0, TimeSpan.FromHours(6)) };

        var bundle = V2ToFhirMapper.Build(discharge);

        var encounterEntry = bundle.Entry.Single(e => e.Resource is Encounter);
        encounterEntry.Request.Method.ShouldBe(Bundle.HTTPVerb.PUT);
        var encounter = (Encounter)encounterEntry.Resource;
        encounter.Status.ShouldBe(Encounter.EncounterStatus.Finished);
        encounter.Period.End.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void No_visit_number_means_no_Encounter_entry_at_all()
    {
        var bundle = V2ToFhirMapper.Build(Admit(visitNumber: null));

        bundle.Entry.Select(e => e.Resource.TypeName).ShouldBe(["Patient"]);
    }

    [Theory]
    [InlineData("M", AdministrativeGender.Male)]
    [InlineData("F", AdministrativeGender.Female)]
    [InlineData("O", AdministrativeGender.Other)]
    [InlineData("U", AdministrativeGender.Unknown)]
    public void HL7_v2_Table_0001_gender_maps_to_AdministrativeGender(string v2, AdministrativeGender expected)
    {
        var patient = (Patient)V2ToFhirMapper.Build(Admit() with { Gender = v2 }).Entry
            .Single(e => e.Resource is Patient).Resource;

        patient.Gender.ShouldBe(expected);
    }

    [Theory]
    [InlineData("I", "IMP")]
    [InlineData("O", "AMB")]
    [InlineData("E", "EMER")]
    [InlineData("Z", "AMB")] // unmapped falls back rather than failing the message
    public void HL7_v2_Table_0004_patient_class_maps_to_v3_ActCode(string v2, string expected)
    {
        var encounter = (Encounter)V2ToFhirMapper.Build(Admit() with { EncounterClass = v2 }).Entry
            .Single(e => e.Resource is Encounter).Resource;

        encounter.Class.Code.ShouldBe(expected);
        encounter.Class.System.ShouldBe(V2Systems.ActCode);
    }

    [Fact]
    public void BirthDate_converts_from_v2_YYYYMMDD_to_FHIR_date()
    {
        var patient = (Patient)V2ToFhirMapper.Build(Admit()).Entry.Single(e => e.Resource is Patient).Resource;

        patient.BirthDate.ShouldBe("1995-06-15");
    }
}
