// Firely's R4 model lazily initialises its list properties and is nullable-
// oblivious; this builder only constructs models, so strict nullability here
// is noise.
#nullable disable

using Hl7.Fhir.Model;
using Prohori.Api.Models;

namespace Prohori.Api.Fhir;

/// <summary>
/// Builds a BD-Core-FHIR-IG conformant transaction Bundle for one field case:
/// Organization + Practitioner + Patient + Encounter + Observation (+ Condition
/// when positive). Every resource declares its BD-Core profile.
/// </summary>
public static class BdCoreBundleBuilder
{
    private static Coding DemoTag => new(Systems.ProhoriTag, Systems.DemoCohortCode) { Display = "Prohori demo cohort" };

    public static Bundle Build(BdCoreCaseSubmission s)
    {
        var orgUrn = "urn:uuid:" + Guid.NewGuid();
        var practitionerUrn = "urn:uuid:" + Guid.NewGuid();
        var patientUrn = "urn:uuid:" + Guid.NewGuid();
        var encounterUrn = "urn:uuid:" + Guid.NewGuid();
        var observationUrn = "urn:uuid:" + Guid.NewGuid();
        var positive = s.RdtResult == RdtResult.Positive;
        var visit = s.VisitDate;

        // --- Organization (reporting facility) ---
        var organization = Profiled<Organization>(BdCore.Organization);
        organization.Identifier.Add(new Identifier(BdCore.FacilityCodeSystem, s.Facility.Code)
        {
            Type = new CodeableConcept(BdCore.V2_0203, "FI", "Facility ID"),
        });
        organization.Active = true;
        organization.Name = s.Facility.Name;

        // --- Practitioner ---
        var practitioner = Profiled<Practitioner>(BdCore.Practitioner);
        practitioner.Identifier.Add(new Identifier(BdCore.HrisSystem, s.PractitionerCode)
        {
            Type = new CodeableConcept(BdCore.V2_0203, "PRN", "Provider number"),
        });

        // --- Patient ---
        var patient = Profiled<Patient>(BdCore.Patient);
        patient.Meta.Tag.Add(DemoTag);
        patient.Identifier.Add(new Identifier { System = BdCore.UhidSystem, Value = "UHID-" + s.Patient.NationalId });
        patient.Identifier.Add(new Identifier(BdCore.NidSystem, s.Patient.NationalId)
        {
            Type = new CodeableConcept(BdCore.IdentifierType, "NID")
            {
                // bd-patient v0.4.6 pins identifier:NID.type.text to this literal
                // (an IG copy-paste artefact — matched here for conformance).
                Text = "Organization identifier",
            },
        });

        var name = new HumanName { Use = HumanName.NameUse.Official, Text = s.Patient.NameEnglish };
        name.TextElement.Extension.Add(new Extension(BdCore.NameEnExt, new FhirString(s.Patient.NameEnglish)));
        if (!string.IsNullOrWhiteSpace(s.Patient.NameBangla))
            name.TextElement.Extension.Add(new Extension(BdCore.NameBnExt, new FhirString(s.Patient.NameBangla)));
        patient.Name.Add(name);

        patient.Gender = Enum.Parse<AdministrativeGender>(s.Patient.Gender, ignoreCase: true);
        patient.BirthDate = s.Patient.BirthDate.ToString("yyyy-MM-dd");

        var address = new Address
        {
            Use = Address.AddressUse.Home,
            District = s.Patient.DistrictCode,
            Country = "BD",
        };
        address.Extension.Add(new Extension(BdCore.DivisionExt,
            new CodeableConcept(BdCore.GeoCodes, s.Patient.DivisionCode)));
        address.Extension.Add(new Extension(BdCore.UpazilaExt,
            new CodeableConcept(BdCore.GeoCodes, s.Patient.UpazilaCode)));
        patient.Address.Add(address);

        // --- Encounter ---
        var encounter = Profiled<Encounter>(BdCore.Encounter);
        encounter.Meta.Tag.Add(DemoTag);
        encounter.Identifier.Add(new Identifier(BdCore.EncounterIdSystem, "ENC-" + Guid.NewGuid().ToString("N")[..12]));
        encounter.Status = Encounter.EncounterStatus.Finished;
        encounter.Class = new Coding(Systems.ActCode, "AMB", "ambulatory");
        encounter.Subject = Ref(patientUrn, s.Patient.NameEnglish);
        encounter.Participant.Add(new Encounter.ParticipantComponent
        {
            Period = new Period { Start = visit.ToString("o"), End = visit.AddMinutes(30).ToString("o") },
            Individual = new ResourceReference(practitionerUrn),
        });
        encounter.ServiceProvider = new ResourceReference(orgUrn) { Display = s.Facility.Name };

        // --- Observation (RDT result) ---
        var (loinc, testDisplay) = LoincFor(s.Disease);
        var observation = Profiled<Observation>(BdCore.Observation);
        observation.Meta.Tag.Add(DemoTag);
        observation.Identifier.Add(new Identifier(BdCore.EncounterIdSystem + "/obs", "OBS-" + Guid.NewGuid().ToString("N")[..12]));
        observation.Status = ObservationStatus.Final;
        observation.Category.Add(new CodeableConcept(Systems.ObservationCategory, "laboratory"));
        observation.Code = new CodeableConcept(Systems.Loinc, loinc, null, testDisplay);
        observation.Subject = Ref(patientUrn, s.Patient.NameEnglish);
        observation.Encounter = new ResourceReference(encounterUrn);
        observation.Effective = new FhirDateTime(visit);
        observation.Performer.Add(new ResourceReference(practitionerUrn));
        observation.Value = new CodeableConcept(Systems.Snomed,
            positive ? "10828004" : "260385009", null, positive ? "Positive" : "Negative");

        // The diagnosis is recorded as Encounter.reasonCode (ICD-11 MMS), not a
        // separate bd-condition: bd-condition v0.4.6 binds Condition.code
        // (required) to an ICD-11 ValueSet that ships EMPTY, so no bd-condition
        // is acceptable to the DGHS sandbox. See docs/phase-f-notes.md.
        if (positive)
        {
            var icd = BdCore.Icd11For(s.Disease);
            encounter.ReasonCode.Add(new CodeableConcept(BdCore.Icd11Mms, icd.Code, icd.Display));
        }

        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };
        bundle.Entry.Add(Post(orgUrn, organization, "Organization",
            ifNoneExist: $"identifier={BdCore.FacilityCodeSystem}|{s.Facility.Code}"));
        bundle.Entry.Add(Post(practitionerUrn, practitioner, "Practitioner",
            ifNoneExist: $"identifier={BdCore.HrisSystem}|{s.PractitionerCode}"));
        bundle.Entry.Add(Post(patientUrn, patient, "Patient",
            ifNoneExist: $"identifier={BdCore.NidSystem}|{s.Patient.NationalId}"));
        bundle.Entry.Add(Post(encounterUrn, encounter, "Encounter"));
        bundle.Entry.Add(Post(observationUrn, observation, "Observation"));

        return bundle;
    }

    private static T Profiled<T>(string profileUrl) where T : Resource, new()
        => new() { Meta = new Meta { Profile = new[] { profileUrl } } };

    private static ResourceReference Ref(string urn, string display) => new(urn) { Display = display };

    private static Bundle.EntryComponent Post(string fullUrl, Resource resource, string type, string ifNoneExist = null)
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

    private static (string Loinc, string Display) LoincFor(Disease d) => d switch
    {
        Disease.Dengue => ("42239-4", "Dengue virus NS1 Ag [Presence] in Serum or Plasma by Immunoassay"),
        Disease.Malaria => ("70048-1", "Plasmodium sp Ag [Presence] in Blood by Rapid immunoassay"),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown disease"),
    };

    private static (string Code, string Display) SnomedFor(Disease d) => d switch
    {
        Disease.Dengue => ("38362002", "Dengue fever"),
        Disease.Malaria => ("84058000", "Malaria"),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown disease"),
    };
}
