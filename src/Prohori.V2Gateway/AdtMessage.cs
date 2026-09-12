namespace Prohori.V2Gateway;

/// <summary>
/// What Prohori pulls out of an ADT^A01/A08/A03 message — a small, FHIR-agnostic slice of
/// PID/PV1/MSH, not the full HL7 v2 object model. <see cref="V2Parser"/> produces one from raw
/// ER7 text; <see cref="V2ToFhirMapper"/> turns it into a Bundle. Keeping this in the middle
/// means neither NHapi's API nor Firely's ever has to know about the other.
/// </summary>
public sealed record AdtMessage
{
    /// <summary>MSH-9-2 — "A01" (admit), "A08" (update), "A03" (discharge), ...</summary>
    public required string TriggerEvent { get; init; }

    /// <summary>PID-3-1 — the Medical Record Number. Prohori's stand-in for a real MPI match.</summary>
    public required string Mrn { get; init; }

    public required string FamilyName { get; init; }
    public string[] GivenNames { get; init; } = [];

    /// <summary>PID-8-1 — HL7 v2 Table 0001 (M/F/O/U/A/N), mapped to FHIR's AdministrativeGender.</summary>
    public string? Gender { get; init; }

    /// <summary>PID-7-1 — YYYYMMDD (or longer), mapped to a FHIR date.</summary>
    public string? BirthDate { get; init; }

    public string? City { get; init; }
    public string? District { get; init; }

    /// <summary>PV1-19-1 — Visit Number, Prohori's Encounter identifier. Absent on some A08s.</summary>
    public string? VisitNumber { get; init; }

    /// <summary>PV1-2-1 — HL7 v2 Table 0004 (I/O/E/...), mapped to FHIR's v3-ActCode.</summary>
    public string? EncounterClass { get; init; }

    public DateTimeOffset? AdmitDateTime { get; init; }
    public DateTimeOffset? DischargeDateTime { get; init; }
}
