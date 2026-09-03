namespace Prohori.Api.Fhir;

/// <summary>
/// Canonicals and code systems from BD-Core-FHIR-IG (bd.fhir.core#0.4.6),
/// Bangladesh's national FHIR profile package — https://fhir.dghs.gov.bd/core/
/// </summary>
public static class BdCore
{
    private const string Base = "https://fhir.dghs.gov.bd/core";

    // Profiles
    public const string Patient = Base + "/StructureDefinition/bd-patient";
    public const string Encounter = Base + "/StructureDefinition/bd-encounter";
    public const string Observation = Base + "/StructureDefinition/bd-observation";
    public const string Condition = Base + "/StructureDefinition/bd-condition";
    public const string Organization = Base + "/StructureDefinition/bd-organization";
    public const string Practitioner = Base + "/StructureDefinition/bd-practitioner";

    // Extensions
    public const string NameEnExt = Base + "/StructureDefinition/bd-name-en";
    public const string NameBnExt = Base + "/StructureDefinition/bd-name-bn";
    public const string DivisionExt = Base + "/StructureDefinition/bd-divisions";
    public const string UpazilaExt = Base + "/StructureDefinition/bd-upazillas";

    // Code / identifier systems
    public const string GeoCodes = Base + "/CodeSystem/bd-geocodes";
    public const string IdentifierType = Base + "/CodeSystem/bd-identifier-type";
    public const string UhidSystem = "http://dghs.gov.bd/identifier/uhid";
    public const string NidSystem = "http://dghs.gov.bd/identifier/nid";
    public const string EncounterIdSystem = Base + "/identifier/encounter";
    public const string FacilityCodeSystem = "http://hrm.dghs.gov.bd/facilities/code";
    public const string HrisSystem = "http://hrm.dghs.gov.bd/practitioners/code";
    public const string Icd11Mms = "http://id.who.int/icd/release/11/mms";
    public const string V2_0203 = "http://terminology.hl7.org/CodeSystem/v2-0203";

    /// <summary>ICD-11 MMS stem code for the two diseases Prohori tracks.</summary>
    public static (string Code, string Display) Icd11For(Models.Disease d) => d switch
    {
        Models.Disease.Dengue => ("1D40", "Dengue fever"),
        Models.Disease.Malaria => ("1F4Z", "Malaria, unspecified"),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unknown disease"),
    };
}
