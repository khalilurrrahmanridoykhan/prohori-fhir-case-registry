using Prohori.V2Gateway;

namespace Prohori.V2Gateway.Tests;

public class V2ParserTests
{
    private static string Adt(string trigger = "A01", string pv1Extra = "") => string.Join("\r",
    [
        $"MSH|^~\\&|PROHORI|DHMC|FHIRGW|PROHORI|20260814090000||ADT^{trigger}|MSG00001|P|2.5.1",
        $"EVN|{trigger}|20260814090000",
        "PID|1||MRN100234^^^DHMC^MR||Khan^Rahman||19950615|M|||Dhaka^^Dhaka^Dhaka^^BD",
        $"PV1|1|I|WARD3^^^DHMC||||1234^Ahmed^Fatima||||||||||||VN5001|||||||||||||||||||||||||20260814090000{pv1Extra}",
        "",
    ]);

    [Fact]
    public void Parses_the_trigger_event_from_MSH_9_2()
    {
        V2Parser.Parse(Adt("A01")).TriggerEvent.ShouldBe("A01");
        V2Parser.Parse(Adt("A08")).TriggerEvent.ShouldBe("A08");
    }

    [Fact]
    public void Parses_patient_demographics_from_PID()
    {
        var message = V2Parser.Parse(Adt());

        message.Mrn.ShouldBe("MRN100234");
        message.FamilyName.ShouldBe("Khan");
        message.GivenNames.ShouldBe(["Rahman"]);
        message.Gender.ShouldBe("M");
        message.BirthDate.ShouldBe("19950615");
        message.City.ShouldBe("Dhaka");
        message.District.ShouldBe("Dhaka");
    }

    [Fact]
    public void Parses_the_visit_and_admit_time_from_PV1()
    {
        var message = V2Parser.Parse(Adt());

        message.VisitNumber.ShouldBe("VN5001");
        message.EncounterClass.ShouldBe("I");
        message.AdmitDateTime.ShouldBe(new DateTimeOffset(2026, 8, 14, 9, 0, 0, TimeSpan.FromHours(6)));
        message.DischargeDateTime.ShouldBeNull();
    }

    [Fact]
    public void A_message_with_no_PV1_still_parses_the_patient()
    {
        var noVisit = string.Join("\r",
        [
            "MSH|^~\\&|PROHORI|DHMC|FHIRGW|PROHORI|20260814090000||ADT^A08|MSG00002|P|2.5.1",
            "EVN|A08|20260814090000",
            "PID|1||MRN100234^^^DHMC^MR||Khan^Rahman||19950615|M|||Dhaka^^Dhaka^^^BD",
            "",
        ]);

        var message = V2Parser.Parse(noVisit);

        message.Mrn.ShouldBe("MRN100234");
        message.VisitNumber.ShouldBeNull();
    }

    [Fact]
    public void Missing_MSH_is_a_FormatException_not_an_unhandled_parser_crash()
    {
        Should.Throw<FormatException>(() => V2Parser.Parse("not an hl7 message"));
    }
}
