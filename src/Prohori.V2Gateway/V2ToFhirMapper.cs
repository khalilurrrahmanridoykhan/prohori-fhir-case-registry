using Hl7.Fhir.Model;

namespace Prohori.V2Gateway;

/// <summary>Canonicals this gateway writes. Prohori-owned — a real deployment would use the
/// sending facility's actual MRN/visit-number identifier systems instead.</summary>
public static class V2Systems
{
    public const string Mrn = "https://prohori.health/fhir/identifier/v2-mrn";
    public const string VisitNumber = "https://prohori.health/fhir/identifier/v2-visit-number";
    public const string ActCode = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
}

/// <summary>
/// Turns an <see cref="AdtMessage"/> into a FHIR R4 transaction Bundle — the declared mapping
/// (PID → Patient, PV1 → Encounter) lives as a real StructureMap in
/// <c>ig/input/fsh/HL7v2ToFhir.fsh</c>; this is the executable side of it, the same relationship
/// CaseBundleBuilder has to no formal declaration at all. Pure function — no I/O.
/// <para/>
/// Idempotency follows the trigger event, not a blanket rule: <b>A01</b> (admit) conditionally
/// <i>creates</i> Patient and Encounter (a retry of the same admit must not duplicate them);
/// <b>A08</b>/<b>A03</b> (update/discharge) conditionally <i>update</i> both by their v2
/// identifiers instead, so a demographic correction or a discharge actually lands on the
/// existing records rather than silently doing nothing.
/// </summary>
public static class V2ToFhirMapper
{
    public static Bundle Build(AdtMessage m)
    {
        var patientUrn = "urn:uuid:" + Guid.NewGuid();
        var isUpdate = m.TriggerEvent is "A08" or "A03";

        var patient = new Patient
        {
            Identifier = [new Identifier(V2Systems.Mrn, m.Mrn)],
            Name = [new HumanName { Family = m.FamilyName, Given = m.GivenNames }],
            Gender = ToAdministrativeGender(m.Gender),
            BirthDate = ToFhirDate(m.BirthDate),
            Address = (m.City, m.District) is (null, null) ? [] :
            [
                new Address { City = m.City, District = m.District, Country = "Bangladesh" },
            ],
        };

        var bundle = new Bundle { Type = Bundle.BundleType.Transaction };
        bundle.Entry.Add(new Bundle.EntryComponent
        {
            FullUrl = patientUrn,
            Resource = patient,
            Request = isUpdate
                ? new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = $"Patient?identifier={V2Systems.Mrn}|{m.Mrn}" }
                : new Bundle.RequestComponent { Method = Bundle.HTTPVerb.POST, Url = "Patient", IfNoneExist = $"identifier={V2Systems.Mrn}|{m.Mrn}" },
        });

        if (m.VisitNumber is { } visitNumber)
        {
            var discharged = m.TriggerEvent == "A03" || m.DischargeDateTime is not null;
            var encounter = new Encounter
            {
                Identifier = [new Identifier(V2Systems.VisitNumber, visitNumber)],
                Status = discharged ? Encounter.EncounterStatus.Finished : Encounter.EncounterStatus.InProgress,
                Class = new Coding(V2Systems.ActCode, ToActCode(m.EncounterClass)),
                Subject = new ResourceReference(patientUrn),
                Period = m.AdmitDateTime is null && m.DischargeDateTime is null ? null : new Period
                {
                    Start = m.AdmitDateTime?.ToString("o"),
                    End = m.DischargeDateTime?.ToString("o"),
                },
            };
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                FullUrl = "urn:uuid:" + Guid.NewGuid(),
                Resource = encounter,
                Request = isUpdate
                    ? new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = $"Encounter?identifier={V2Systems.VisitNumber}|{visitNumber}" }
                    : new Bundle.RequestComponent { Method = Bundle.HTTPVerb.POST, Url = "Encounter", IfNoneExist = $"identifier={V2Systems.VisitNumber}|{visitNumber}" },
            });
        }

        return bundle;
    }

    /// <summary>HL7 v2 Table 0001 -> FHIR AdministrativeGender.</summary>
    private static AdministrativeGender? ToAdministrativeGender(string? v2Gender) => v2Gender switch
    {
        "M" => AdministrativeGender.Male,
        "F" => AdministrativeGender.Female,
        "O" => AdministrativeGender.Other,
        "U" or "A" or "N" => AdministrativeGender.Unknown,
        _ => null,
    };

    private static string? ToFhirDate(string? v2Date)
    {
        if (string.IsNullOrEmpty(v2Date) || v2Date.Length < 8) return null;
        return DateTime.TryParseExact(v2Date[..8], "yyyyMMdd",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd")
            : null;
    }

    /// <summary>HL7 v2 Table 0004 (patient class) -> FHIR v3-ActCode. Unmapped/unknown codes
    /// fall back to AMB rather than failing the whole message — a facility-specific patient
    /// class the mapper doesn't know about shouldn't block the admit.</summary>
    private static string ToActCode(string? v2Class) => v2Class switch
    {
        "I" => "IMP",
        "O" => "AMB",
        "E" => "EMER",
        "P" => "PRENC",
        "R" => "AMB",
        "B" => "AMB",
        _ => "AMB",
    };
}
