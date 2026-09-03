namespace Prohori.Api.Fhir;

/// <summary>Canonical URIs for the code systems and identifier namespaces Prohori uses.</summary>
public static class Systems
{
    public const string NationalId = "http://health.gov.bd/sid";
    public const string Loinc = "http://loinc.org";
    public const string Snomed = "http://snomed.info/sct";
    public const string Icd10 = "http://hl7.org/fhir/sid/icd-10";
    public const string ObservationCategory = "http://terminology.hl7.org/CodeSystem/observation-category";
    public const string ConditionClinical = "http://terminology.hl7.org/CodeSystem/condition-clinical";
    public const string ConditionVerStatus = "http://terminology.hl7.org/CodeSystem/condition-ver-status";
    public const string ActCode = "http://terminology.hl7.org/CodeSystem/v3-ActCode";

    /// <summary>Tag written on every resource so the cohort is findable on a shared server.</summary>
    public const string ProhoriTag = "urn:prohori";
    public const string DemoCohortCode = "demo-cohort";
}
