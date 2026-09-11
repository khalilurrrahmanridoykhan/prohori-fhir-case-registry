using Hl7.Fhir.Model;
using Prohori.Api.Models;

namespace Prohori.Api.Fhir;

/// <summary>
/// SDC's two operations, hand-implemented against <see cref="QuestionnaireCatalog"/>:
/// <c>$extract</c> turns a filled-in <see cref="QuestionnaireResponse"/> into the same
/// <see cref="CaseSubmission"/> the typed <c>/cases</c> endpoint accepts — so the Bundle itself
/// is still built by <see cref="CaseBundleBuilder"/>, not re-implemented here — and <c>$populate</c>
/// turns an existing <see cref="Patient"/> back into a prefilled response for a returning patient's
/// next visit. Both are pure functions: no I/O, trivially unit-testable.
/// </summary>
public static class QuestionnaireExtraction
{
    public static CaseSubmission Extract(QuestionnaireResponse response)
    {
        var byLinkId = Flatten(response.Item);
        var errors = new Dictionary<string, string[]>();

        var nationalId = StringOf(byLinkId, QuestionnaireLinkIds.NationalId);
        if (nationalId is null || !System.Text.RegularExpressions.Regex.IsMatch(nationalId, @"^\d{10,17}$"))
            errors[QuestionnaireLinkIds.NationalId] = ["Required, 10–17 digits."];

        var familyName = StringOf(byLinkId, QuestionnaireLinkIds.FamilyName);
        if (string.IsNullOrWhiteSpace(familyName))
            errors[QuestionnaireLinkIds.FamilyName] = ["Required."];

        var gender = CodeOf(byLinkId, QuestionnaireLinkIds.Gender);
        if (gender is null || !Enum.TryParse<AdministrativeGender>(gender, ignoreCase: true, out _))
            errors[QuestionnaireLinkIds.Gender] = ["Required: male, female, other or unknown."];

        var birthDate = DateOnlyOf(byLinkId, QuestionnaireLinkIds.BirthDate);
        if (birthDate is null)
            errors[QuestionnaireLinkIds.BirthDate] = ["Required, a valid date."];

        var city = StringOf(byLinkId, QuestionnaireLinkIds.City);
        if (string.IsNullOrWhiteSpace(city))
            errors[QuestionnaireLinkIds.City] = ["Required."];

        var district = StringOf(byLinkId, QuestionnaireLinkIds.District);
        if (string.IsNullOrWhiteSpace(district))
            errors[QuestionnaireLinkIds.District] = ["Required."];

        var disease = DiseaseOf(byLinkId, QuestionnaireLinkIds.Disease);
        if (disease is null)
            errors[QuestionnaireLinkIds.Disease] = ["Required: dengue (SNOMED 38362002) or malaria (SNOMED 84058000)."];

        var rdtResult = RdtResultOf(byLinkId, QuestionnaireLinkIds.RdtResult);
        if (rdtResult is null)
            errors[QuestionnaireLinkIds.RdtResult] = ["Required: positive (SNOMED 10828004) or negative (SNOMED 260385009)."];

        var visitDate = DateTimeOf(byLinkId, QuestionnaireLinkIds.VisitDate);
        if (visitDate is null)
            errors[QuestionnaireLinkIds.VisitDate] = ["Required, a valid date/time."];

        if (errors.Count > 0) throw new QuestionnaireExtractionException(errors);

        return new CaseSubmission
        {
            Patient = new PatientInput
            {
                NationalId = nationalId!,
                FamilyName = familyName!,
                GivenNames = StringsOf(byLinkId, QuestionnaireLinkIds.GivenNames),
                Gender = gender!.ToLowerInvariant(),
                BirthDate = birthDate!.Value,
                City = city!,
                District = district!,
            },
            Disease = disease!.Value,
            RdtResult = rdtResult!.Value,
            VisitDate = visitDate!.Value,
        };
    }

    /// <summary>A blank, in-progress response with the patient-demographics group prefilled from
    /// an existing Patient — so a community health worker recording a follow-up visit does not
    /// retype what the registry already knows.</summary>
    public static QuestionnaireResponse Populate(Patient? patient) => new()
    {
        Questionnaire = QuestionnaireCatalog.CanonicalUrl,
        Status = QuestionnaireResponse.QuestionnaireResponseStatus.InProgress,
        Subject = patient?.Id is { } id ? new ResourceReference($"Patient/{id}") : null,
        Item = patient is null ? [] :
        [
            new QuestionnaireResponse.ItemComponent
            {
                LinkId = QuestionnaireLinkIds.Patient,
                Item =
                [
                    .. StringAnswer(QuestionnaireLinkIds.NationalId,
                        patient.Identifier.FirstOrDefault(i => i.System == Systems.NationalId)?.Value),
                    .. StringAnswer(QuestionnaireLinkIds.FamilyName, patient.Name.FirstOrDefault()?.Family),
                    .. RepeatingStringAnswer(QuestionnaireLinkIds.GivenNames, patient.Name.FirstOrDefault()?.Given),
                    .. CodingAnswer(QuestionnaireLinkIds.Gender,
                        patient.Gender is { } g ? new Coding("http://hl7.org/fhir/administrative-gender", g.ToString().ToLowerInvariant()) : null),
                    .. DateAnswer(QuestionnaireLinkIds.BirthDate, patient.BirthDate),
                    .. StringAnswer(QuestionnaireLinkIds.City, patient.Address.FirstOrDefault()?.City),
                    .. StringAnswer(QuestionnaireLinkIds.District, patient.Address.FirstOrDefault()?.District),
                ],
            },
        ],
    };

    private static IEnumerable<QuestionnaireResponse.ItemComponent> StringAnswer(string linkId, string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] :
        [new QuestionnaireResponse.ItemComponent { LinkId = linkId, Answer = [new() { Value = new FhirString(value) }] }];

    private static IEnumerable<QuestionnaireResponse.ItemComponent> RepeatingStringAnswer(string linkId, IEnumerable<string?>? values)
    {
        var answers = (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => new QuestionnaireResponse.AnswerComponent { Value = new FhirString(v!) }).ToList();
        return answers.Count == 0 ? [] : [new QuestionnaireResponse.ItemComponent { LinkId = linkId, Answer = answers }];
    }

    private static IEnumerable<QuestionnaireResponse.ItemComponent> CodingAnswer(string linkId, Coding? value) =>
        value is null ? [] :
        [new QuestionnaireResponse.ItemComponent { LinkId = linkId, Answer = [new() { Value = value }] }];

    private static IEnumerable<QuestionnaireResponse.ItemComponent> DateAnswer(string linkId, string? isoDate) =>
        string.IsNullOrWhiteSpace(isoDate) ? [] :
        [new QuestionnaireResponse.ItemComponent { LinkId = linkId, Answer = [new() { Value = new Date(isoDate) }] }];

    private static Dictionary<string, QuestionnaireResponse.ItemComponent> Flatten(
        IEnumerable<QuestionnaireResponse.ItemComponent>? items, Dictionary<string, QuestionnaireResponse.ItemComponent>? into = null)
    {
        var result = into ?? [];
        foreach (var item in items ?? [])
        {
            if (item.LinkId != null) result[item.LinkId] = item;
            Flatten(item.Item, result);
            foreach (var answer in item.Answer ?? []) Flatten(answer.Item, result);
        }
        return result;
    }

    private static string? StringOf(IReadOnlyDictionary<string, QuestionnaireResponse.ItemComponent> items, string linkId) =>
        items.TryGetValue(linkId, out var item) ? (item.Answer?.FirstOrDefault()?.Value as FhirString)?.Value : null;

    private static string[] StringsOf(IReadOnlyDictionary<string, QuestionnaireResponse.ItemComponent> items, string linkId) =>
        items.TryGetValue(linkId, out var item)
            ? item.Answer?.Select(a => (a.Value as FhirString)?.Value).OfType<string>().ToArray() ?? []
            : [];

    private static string? CodeOf(IReadOnlyDictionary<string, QuestionnaireResponse.ItemComponent> items, string linkId) =>
        items.TryGetValue(linkId, out var item) ? (item.Answer?.FirstOrDefault()?.Value as Coding)?.Code : null;

    private static DateOnly? DateOnlyOf(IReadOnlyDictionary<string, QuestionnaireResponse.ItemComponent> items, string linkId)
    {
        var raw = items.TryGetValue(linkId, out var item) ? (item.Answer?.FirstOrDefault()?.Value as Date)?.Value : null;
        return raw != null && DateOnly.TryParse(raw, out var value) ? value : null;
    }

    private static DateTimeOffset? DateTimeOf(IReadOnlyDictionary<string, QuestionnaireResponse.ItemComponent> items, string linkId)
    {
        var raw = items.TryGetValue(linkId, out var item) ? (item.Answer?.FirstOrDefault()?.Value as FhirDateTime)?.Value : null;
        return raw != null && DateTimeOffset.TryParse(raw, out var value) ? value : null;
    }

    private static Disease? DiseaseOf(IReadOnlyDictionary<string, QuestionnaireResponse.ItemComponent> items, string linkId) =>
        CodeOf(items, linkId) switch { "38362002" => Disease.Dengue, "84058000" => Disease.Malaria, _ => null };

    private static RdtResult? RdtResultOf(IReadOnlyDictionary<string, QuestionnaireResponse.ItemComponent> items, string linkId) =>
        CodeOf(items, linkId) switch { "10828004" => RdtResult.Positive, "260385009" => RdtResult.Negative, _ => null };
}

/// <summary>Thrown when a <see cref="QuestionnaireResponse"/> is missing or misanswers a required item.</summary>
public sealed class QuestionnaireExtractionException(Dictionary<string, string[]> errors)
    : Exception("The QuestionnaireResponse is missing required answers.")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
