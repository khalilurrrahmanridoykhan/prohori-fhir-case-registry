using Hl7.Fhir.Model;

namespace Prohori.Api.Fhir;

/// <summary>
/// The linkIds shared by <see cref="QuestionnaireCatalog"/> (what the form declares) and
/// <see cref="QuestionnaireExtraction"/> (what reads the answers back out). Keeping them as
/// constants — rather than magic strings in two files — is what keeps $populate and $extract
/// honest about the same shape.
/// </summary>
public static class QuestionnaireLinkIds
{
    public const string Patient = "patient";
    public const string NationalId = "patient.nationalId";
    public const string FamilyName = "patient.familyName";
    public const string GivenNames = "patient.givenNames";
    public const string Gender = "patient.gender";
    public const string BirthDate = "patient.birthDate";
    public const string City = "patient.city";
    public const string District = "patient.district";
    public const string Disease = "disease";
    public const string RdtResult = "rdtResult";
    public const string VisitDate = "visitDate";
    public const string DiagnosisNote = "diagnosisNote";
}

/// <summary>
/// Builds the one <c>Questionnaire</c> Prohori's field intake is authored against: patient
/// demographics, disease, RDT result and visit date — the same facts <see cref="CaseBundleBuilder"/>
/// needs, just captured as a form instead of a typed request body. <c>disease</c> and
/// <c>rdtResult</c> carry the identical SNOMED codes <see cref="CaseBundleBuilder"/> writes onto
/// the Observation, so extraction is a lookup, not a translation.
/// <para/>
/// Unlike <c>ProhoriPatient</c> (a FHIR Shorthand <b>profile</b> constraining instances a server
/// receives), this Questionnaire has no separate "real-world instance" to constrain — the
/// resource returned here <i>is</i> the artifact. It is authored in C#, not FSH, so there is one
/// place a linkId can go stale, not two; see DECISIONS.md.
/// </summary>
public static class QuestionnaireCatalog
{
    public const string CanonicalUrl = "https://prohori.health/fhir/Questionnaire/prohori-case-questionnaire";

    public static Questionnaire Build() => new()
    {
        Url = CanonicalUrl,
        Version = "0.1.0",
        Name = "ProhoriCaseQuestionnaire",
        Title = "Prohori field case intake",
        Status = PublicationStatus.Draft,
        SubjectType = [ResourceType.Patient],
        Date = "2026",
        Item =
        [
            new Questionnaire.ItemComponent
            {
                LinkId = QuestionnaireLinkIds.Patient,
                Text = "Patient",
                Type = Questionnaire.QuestionnaireItemType.Group,
                Item =
                [
                    StringItem(QuestionnaireLinkIds.NationalId, "Bangladesh National ID (NID)", required: true,
                        regex: @"^\d{10,17}$"),
                    StringItem(QuestionnaireLinkIds.FamilyName, "Family name", required: true),
                    StringItem(QuestionnaireLinkIds.GivenNames, "Given name(s)", required: false, repeats: true),
                    ChoiceItem(QuestionnaireLinkIds.Gender, "Gender", required: true,
                        ("male", "Male"), ("female", "Female"), ("other", "Other"), ("unknown", "Unknown")),
                    new Questionnaire.ItemComponent
                    {
                        LinkId = QuestionnaireLinkIds.BirthDate,
                        Text = "Date of birth",
                        Type = Questionnaire.QuestionnaireItemType.Date,
                        Required = true,
                    },
                    StringItem(QuestionnaireLinkIds.City, "City / upazila", required: true),
                    StringItem(QuestionnaireLinkIds.District, "District", required: true),
                ],
            },
            new Questionnaire.ItemComponent
            {
                LinkId = QuestionnaireLinkIds.Disease,
                Text = "Suspected disease",
                Type = Questionnaire.QuestionnaireItemType.Choice,
                Required = true,
                AnswerOption =
                [
                    AnswerCoding(Systems.Snomed, "38362002", "Dengue fever"),
                    AnswerCoding(Systems.Snomed, "61462000", "Malaria"),
                ],
            },
            new Questionnaire.ItemComponent
            {
                LinkId = QuestionnaireLinkIds.RdtResult,
                Text = "Rapid diagnostic test result",
                Type = Questionnaire.QuestionnaireItemType.Choice,
                Required = true,
                AnswerOption =
                [
                    AnswerCoding(Systems.Snomed, "10828004", "Positive"),
                    AnswerCoding(Systems.Snomed, "260385009", "Negative"),
                ],
            },
            new Questionnaire.ItemComponent
            {
                LinkId = QuestionnaireLinkIds.VisitDate,
                Text = "Visit date/time",
                Type = Questionnaire.QuestionnaireItemType.DateTime,
                Required = true,
            },
            new Questionnaire.ItemComponent
            {
                LinkId = QuestionnaireLinkIds.DiagnosisNote,
                Text = "Diagnosis note",
                Type = Questionnaire.QuestionnaireItemType.String,
                Required = false,
                // The point of enableWhen: a diagnosis note is meaningless until the RDT is
                // positive, so the item stays hidden — and unanswered — until then.
                EnableWhen =
                [
                    new Questionnaire.EnableWhenComponent
                    {
                        Question = QuestionnaireLinkIds.RdtResult,
                        Operator = Questionnaire.QuestionnaireItemOperator.Equal,
                        Answer = new Coding(Systems.Snomed, "10828004", "Positive"),
                    },
                ],
            },
        ],
    };

    private static Questionnaire.ItemComponent StringItem(string linkId, string text, bool required, bool repeats = false, string? regex = null)
    {
        var item = new Questionnaire.ItemComponent
        {
            LinkId = linkId,
            Text = text,
            Type = Questionnaire.QuestionnaireItemType.String,
            Required = required,
            Repeats = repeats,
        };
        if (regex != null)
            item.AddExtension("http://hl7.org/fhir/StructureDefinition/regex", new FhirString(regex));
        return item;
    }

    private static Questionnaire.ItemComponent ChoiceItem(string linkId, string text, bool required, params (string Code, string Display)[] options)
        => new()
        {
            LinkId = linkId,
            Text = text,
            Type = Questionnaire.QuestionnaireItemType.Choice,
            Required = required,
            AnswerOption = options.Select(o => new Questionnaire.AnswerOptionComponent
            {
                Value = new Coding("http://hl7.org/fhir/administrative-gender", o.Code, o.Display),
            }).ToList(),
        };

    private static Questionnaire.AnswerOptionComponent AnswerCoding(string system, string code, string display)
        => new() { Value = new Coding(system, code, display) };
}
