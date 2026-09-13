using Hl7.Fhir.Model;
using Prohori.Api.Fhir;

namespace Prohori.Api.Tests;

public class QuestionnaireExtractionTests
{
    private static QuestionnaireResponse.ItemComponent Answer(string linkId, DataType value) =>
        new() { LinkId = linkId, Answer = [new() { Value = value }] };

    private static QuestionnaireResponse Valid(
        Disease disease = Disease.Dengue,
        RdtResult result = RdtResult.Positive,
        string nationalId = "19942691012345678",
        string? diagnosisNote = null) => new()
        {
            Questionnaire = QuestionnaireCatalog.CanonicalUrl,
            Status = QuestionnaireResponse.QuestionnaireResponseStatus.Completed,
            Item =
            [
                new QuestionnaireResponse.ItemComponent
                {
                    LinkId = QuestionnaireLinkIds.Patient,
                    Item =
                    [
                        Answer(QuestionnaireLinkIds.NationalId, new FhirString(nationalId)),
                        Answer(QuestionnaireLinkIds.FamilyName, new FhirString("Khan")),
                        new QuestionnaireResponse.ItemComponent
                        {
                            LinkId = QuestionnaireLinkIds.GivenNames,
                            Answer = [new() { Value = new FhirString("Rahman") }],
                        },
                        Answer(QuestionnaireLinkIds.Gender, new Coding("http://hl7.org/fhir/administrative-gender", "male")),
                        Answer(QuestionnaireLinkIds.BirthDate, new Date("1995-06-15")),
                        Answer(QuestionnaireLinkIds.City, new FhirString("Dhaka")),
                        Answer(QuestionnaireLinkIds.District, new FhirString("Dhaka")),
                    ],
                },
                Answer(QuestionnaireLinkIds.Disease, new Coding(Systems.Snomed,
                    disease == Disease.Dengue ? "38362002" : "61462000")),
                Answer(QuestionnaireLinkIds.RdtResult, new Coding(Systems.Snomed,
                    result == RdtResult.Positive ? "10828004" : "260385009")),
                Answer(QuestionnaireLinkIds.VisitDate, new FhirDateTime("2026-08-14T09:20:00+06:00")),
                .. diagnosisNote is null ? Array.Empty<QuestionnaireResponse.ItemComponent>()
                    : [Answer(QuestionnaireLinkIds.DiagnosisNote, new FhirString(diagnosisNote))],
            ],
        };

    [Fact]
    public void A_fully_answered_response_extracts_the_same_submission_the_typed_endpoint_expects()
    {
        var submission = QuestionnaireExtraction.Extract(Valid());

        submission.Patient.NationalId.ShouldBe("19942691012345678");
        submission.Patient.FamilyName.ShouldBe("Khan");
        submission.Patient.GivenNames.ShouldBe(["Rahman"]);
        submission.Patient.Gender.ShouldBe("male");
        submission.Patient.BirthDate.ShouldBe(new DateOnly(1995, 6, 15));
        submission.Patient.City.ShouldBe("Dhaka");
        submission.Patient.District.ShouldBe("Dhaka");
        submission.Disease.ShouldBe(Disease.Dengue);
        submission.RdtResult.ShouldBe(RdtResult.Positive);
        submission.VisitDate.ShouldBe(DateTimeOffset.Parse("2026-08-14T09:20:00+06:00"));
    }

    [Theory]
    [InlineData(Disease.Dengue, "38362002")]
    [InlineData(Disease.Malaria, "61462000")]
    public void Disease_is_read_back_from_its_SNOMED_answer_coding(Disease disease, string code)
    {
        var submission = QuestionnaireExtraction.Extract(Valid(disease));
        submission.Disease.ShouldBe(disease);
        code.ShouldNotBeNullOrEmpty(); // documents which coding drove the theory case
    }

    [Fact]
    public void The_extracted_submission_builds_the_same_Bundle_as_a_typed_submission()
    {
        var fromQuestionnaire = CaseBundleBuilder.Build(QuestionnaireExtraction.Extract(Valid()));
        var fromTypedApi = CaseBundleBuilder.Build(Sample.Case());

        fromQuestionnaire.Entry.Select(e => e.Resource.TypeName)
            .ShouldBe(fromTypedApi.Entry.Select(e => e.Resource.TypeName));
    }

    [Fact]
    public void A_missing_national_id_is_a_field_error_not_an_exception_stack()
    {
        var response = Valid();
        response.Item[0].Item.RemoveAll(i => i.LinkId == QuestionnaireLinkIds.NationalId);

        var ex = Should.Throw<QuestionnaireExtractionException>(() => QuestionnaireExtraction.Extract(response));

        ex.Errors.Keys.ShouldContain(QuestionnaireLinkIds.NationalId);
    }

    [Fact]
    public void A_non_digit_national_id_is_rejected()
    {
        var response = Valid(nationalId: "not-a-number");

        var ex = Should.Throw<QuestionnaireExtractionException>(() => QuestionnaireExtraction.Extract(response));

        ex.Errors.Keys.ShouldContain(QuestionnaireLinkIds.NationalId);
    }

    [Fact]
    public void An_unanswered_diagnosis_note_on_a_negative_result_is_not_an_error()
    {
        // enableWhen kept the item hidden client-side; the server must not demand it either.
        var submission = QuestionnaireExtraction.Extract(Valid(result: RdtResult.Negative, diagnosisNote: null));

        submission.RdtResult.ShouldBe(RdtResult.Negative);
    }

    [Fact]
    public void Multiple_missing_required_items_are_reported_together()
    {
        var response = Valid();
        response.Item[0].Item.RemoveAll(i => i.LinkId is QuestionnaireLinkIds.FamilyName or QuestionnaireLinkIds.City);

        var ex = Should.Throw<QuestionnaireExtractionException>(() => QuestionnaireExtraction.Extract(response));

        ex.Errors.Keys.ShouldContain(QuestionnaireLinkIds.FamilyName);
        ex.Errors.Keys.ShouldContain(QuestionnaireLinkIds.City);
    }

    [Fact]
    public void Populate_prefills_the_patient_group_from_an_existing_Patient()
    {
        var patient = new Patient
        {
            Id = "123",
            Identifier = [new Identifier(Systems.NationalId, "19942691012345678")],
            Name = [new HumanName { Family = "Khan", Given = ["Rahman"] }],
            Gender = AdministrativeGender.Male,
            BirthDate = "1995-06-15",
            Address = [new Address { City = "Dhaka", District = "Dhaka" }],
        };

        var response = QuestionnaireExtraction.Populate(patient);
        var submission = QuestionnaireExtraction.Extract(MergeVisitAnswers(response));

        response.Subject.Reference.ShouldBe("Patient/123");
        submission.Patient.NationalId.ShouldBe("19942691012345678");
        submission.Patient.FamilyName.ShouldBe("Khan");
        submission.Patient.City.ShouldBe("Dhaka");
    }

    [Fact]
    public void Populate_with_no_matching_patient_returns_a_blank_in_progress_response()
    {
        var response = QuestionnaireExtraction.Populate(null);

        response.Status.ShouldBe(QuestionnaireResponse.QuestionnaireResponseStatus.InProgress);
        response.Item.ShouldBeEmpty();
        response.Subject.ShouldBeNull();
    }

    /// <summary>A populated response only carries patient demographics; add the visit facts a
    /// real form submission would supply before it round-trips through extraction.</summary>
    private static QuestionnaireResponse MergeVisitAnswers(QuestionnaireResponse populated)
    {
        populated.Item.Add(Answer(QuestionnaireLinkIds.Disease, new Coding(Systems.Snomed, "38362002")));
        populated.Item.Add(Answer(QuestionnaireLinkIds.RdtResult, new Coding(Systems.Snomed, "10828004")));
        populated.Item.Add(Answer(QuestionnaireLinkIds.VisitDate, new FhirDateTime("2026-08-14T09:20:00+06:00")));
        return populated;
    }
}
