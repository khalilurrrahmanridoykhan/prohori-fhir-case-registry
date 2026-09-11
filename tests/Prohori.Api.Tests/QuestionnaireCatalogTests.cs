using Hl7.Fhir.Model;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

public class QuestionnaireCatalogTests
{
    [Fact]
    public void Has_a_stable_canonical_url()
    {
        var questionnaire = QuestionnaireCatalog.Build();

        questionnaire.Url.ShouldBe(QuestionnaireCatalog.CanonicalUrl);
        questionnaire.Status.ShouldBe(PublicationStatus.Draft);
    }

    [Fact]
    public void Every_linkId_used_by_extraction_is_declared()
    {
        var linkIds = Flatten(QuestionnaireCatalog.Build().Item).Select(i => i.LinkId).ToHashSet();

        linkIds.ShouldContain(QuestionnaireLinkIds.NationalId);
        linkIds.ShouldContain(QuestionnaireLinkIds.FamilyName);
        linkIds.ShouldContain(QuestionnaireLinkIds.GivenNames);
        linkIds.ShouldContain(QuestionnaireLinkIds.Gender);
        linkIds.ShouldContain(QuestionnaireLinkIds.BirthDate);
        linkIds.ShouldContain(QuestionnaireLinkIds.City);
        linkIds.ShouldContain(QuestionnaireLinkIds.District);
        linkIds.ShouldContain(QuestionnaireLinkIds.Disease);
        linkIds.ShouldContain(QuestionnaireLinkIds.RdtResult);
        linkIds.ShouldContain(QuestionnaireLinkIds.VisitDate);
        linkIds.ShouldContain(QuestionnaireLinkIds.DiagnosisNote);
    }

    [Fact]
    public void Disease_options_carry_the_same_SNOMED_codes_CaseBundleBuilder_writes()
    {
        var item = Flatten(QuestionnaireCatalog.Build().Item).Single(i => i.LinkId == QuestionnaireLinkIds.Disease);

        var codes = item.AnswerOption.Select(o => ((Coding)o.Value).Code).ToArray();
        codes.ShouldBe(["38362002", "84058000"]);
    }

    [Fact]
    public void RdtResult_options_carry_the_same_SNOMED_codes_CaseBundleBuilder_writes()
    {
        var item = Flatten(QuestionnaireCatalog.Build().Item).Single(i => i.LinkId == QuestionnaireLinkIds.RdtResult);

        var codes = item.AnswerOption.Select(o => ((Coding)o.Value).Code).ToArray();
        codes.ShouldBe(["10828004", "260385009"]);
    }

    [Fact]
    public void DiagnosisNote_is_hidden_until_the_RDT_is_positive()
    {
        var item = Flatten(QuestionnaireCatalog.Build().Item).Single(i => i.LinkId == QuestionnaireLinkIds.DiagnosisNote);

        var enableWhen = item.EnableWhen.ShouldHaveSingleItem();
        enableWhen.Question.ShouldBe(QuestionnaireLinkIds.RdtResult);
        enableWhen.Operator.ShouldBe(Questionnaire.QuestionnaireItemOperator.Equal);
        ((Coding)enableWhen.Answer).Code.ShouldBe("10828004");
    }

    [Fact]
    public void National_id_carries_the_same_10_to_17_digit_pattern_CaseSubmission_enforces()
    {
        var item = Flatten(QuestionnaireCatalog.Build().Item).Single(i => i.LinkId == QuestionnaireLinkIds.NationalId);

        var regex = item.GetExtension("http://hl7.org/fhir/StructureDefinition/regex");
        ((FhirString)regex.Value).Value.ShouldBe(@"^\d{10,17}$");
    }

    private static IEnumerable<Questionnaire.ItemComponent> Flatten(IEnumerable<Questionnaire.ItemComponent> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Item)) yield return child;
        }
    }
}
