using Hl7.Fhir.Model;
using Prohori.Api.Models;

namespace Prohori.Api.Fhir;

/// <summary>
/// Turns a <see cref="CaseSubmission"/> into a FHIR R4 <b>transaction</b> Bundle:
/// Patient → Encounter → Observation (RDT result), plus a Condition when the RDT
/// is positive. Pure function — no I/O — so it is trivially unit-testable.
/// </summary>
public static class CaseBundleBuilder
{
    private static Coding DemoTag => new(Systems.ProhoriTag, Systems.DemoCohortCode) { Display = "Prohori demo cohort" };

    public static Bundle Build(CaseSubmission s)
    {
        var patientUrn = "urn:uuid:" + Guid.NewGuid();
        var encounterUrn = "urn:uuid:" + Guid.NewGuid();
        var observationUrn = "urn:uuid:" + Guid.NewGuid();
        var positive = s.RdtResult == RdtResult.Positive;

        var patient = new Patient
        {
            Meta = new Meta { Tag = [DemoTag] },
            Identifier = [new Identifier(Systems.NationalId, s.Patient.NationalId)],
            Active = true,
            Name =
            [
                new HumanName
                {
                    Use = HumanName.NameUse.Official,
                    Family = s.Patient.FamilyName,
                    Given = s.Patient.GivenNames,
                }
            ],
            Gender = Enum.Parse<AdministrativeGender>(s.Patient.Gender, ignoreCase: true),
            BirthDate = s.Patient.BirthDate.ToString("yyyy-MM-dd"),
            Address =
            [
                new Address
                {
                    Use = Address.AddressUse.Home,
                    City = s.Patient.City,
                    District = s.Patient.District,
                    Country = "Bangladesh",
                }
            ],
        };

        var encounter = new Encounter
        {
            Meta = new Meta { Tag = [DemoTag] },
            Status = Encounter.EncounterStatus.Finished,
            Class = new Coding(Systems.ActCode, "FLD", "field"),
            Subject = new ResourceReference(patientUrn),
            Period = new Period
            {
                Start = s.VisitDate.ToString("o"),
                End = s.VisitDate.AddMinutes(30).ToString("o"),
            },
        };

        var (loinc, testDisplay) = TestFor(s.Disease);
        var observation = new Observation
        {
            Meta = new Meta { Tag = [DemoTag] },
            Status = ObservationStatus.Final,
            Category = [new CodeableConcept(Systems.ObservationCategory, "laboratory")],
            Code = new CodeableConcept(Systems.Loinc, loinc, null, testDisplay),
            Subject = new ResourceReference(patientUrn),
            Encounter = new ResourceReference(encounterUrn),
            Effective = new FhirDateTime(s.VisitDate),
            Value = new CodeableConcept(
                Systems.Snomed,
                positive ? "10828004" : "260385009",
                null,
                positive ? "Positive" : "Negative"),
        };

        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };
        bundle.Entry.Add(Post(patientUrn, patient, "Patient",
            ifNoneExist: $"identifier={Systems.NationalId}|{s.Patient.NationalId}"));
        bundle.Entry.Add(Post(encounterUrn, encounter, "Encounter"));
        bundle.Entry.Add(Post(observationUrn, observation, "Observation"));

        if (positive)
        {
            var dx = DiagnosisFor(s.Disease);
            bundle.Entry.Add(Post("urn:uuid:" + Guid.NewGuid(), new Condition
            {
                Meta = new Meta { Tag = [DemoTag] },
                ClinicalStatus = new CodeableConcept(Systems.ConditionClinical, "active"),
                VerificationStatus = new CodeableConcept(Systems.ConditionVerStatus, "confirmed"),
                Code = new CodeableConcept
                {
                    Coding =
                    [
                        new Coding(Systems.Snomed, dx.Snomed, dx.SnomedDisplay),
                        new Coding(Systems.Icd10, dx.Icd10, dx.Icd10Display),
                    ],
                },
                Subject = new ResourceReference(patientUrn),
                Encounter = new ResourceReference(encounterUrn),
                Onset = new FhirDateTime(s.VisitDate),
                RecordedDate = s.VisitDate.ToString("yyyy-MM-dd"),
            }, "Condition"));
        }

        return bundle;
    }

    private static Bundle.EntryComponent Post(string fullUrl, Resource resource, string type, string? ifNoneExist = null)
        => new()
        {
            FullUrl = fullUrl,
            Resource = resource,
            Request = new Bundle.RequestComponent
            {
                Method = Bundle.HTTPVerb.POST,
                Url = type,
                IfNoneExist = ifNoneExist,
            },
        };

    private static (string Loinc, string Display) TestFor(Disease d) => d switch
    {
        Disease.Dengue => ("42239-4", "Dengue virus NS1 Ag [Presence] in Serum or Plasma by Immunoassay"),
        Disease.Malaria => ("70048-1", "Plasmodium sp Ag [Presence] in Blood by Rapid immunoassay"),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown disease"),
    };

    private static (string Snomed, string SnomedDisplay, string Icd10, string Icd10Display) DiagnosisFor(Disease d) => d switch
    {
        Disease.Dengue => ("38362002", "Dengue fever", "A90", "Dengue fever [classical dengue]"),
        Disease.Malaria => ("84058000", "Malaria", "B54", "Unspecified malaria"),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown disease"),
    };
}
