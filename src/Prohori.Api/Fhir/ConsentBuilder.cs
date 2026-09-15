using Hl7.Fhir.Model;

namespace Prohori.Api.Fhir;

/// <summary>
/// Builds the default per-patient <see cref="Consent"/> record — created once,
/// alongside the patient's first case, defaulting to <c>permit</c>. Pure
/// function; <see cref="FhirCaseService"/> adds it to the same transaction
/// Bundle as the case (conditional create, so a second visit doesn't create
/// a second Consent). Toggled later via <c>PUT /patients/{nationalId}/consent</c>.
/// </summary>
public static class ConsentBuilder
{
    public const string PolicyRule = "https://prohori.health/fhir/consent-policy/field-registry";

    /// <param name="patientUrn">The patient's <c>urn:uuid:</c> fullUrl within the same transaction.</param>
    /// <param name="nationalId">
    /// Carried as the Consent's own <see cref="Consent.Identifier"/> (not just a
    /// <see cref="Consent.Patient"/> reference) so a returning patient's next visit can
    /// conditionally match it with a plain <c>identifier=</c> search — no chaining through
    /// the reference required. That's not just a simplification: chaining
    /// (<c>patient.identifier=</c>) is valid FHIR and works as a standalone conditional
    /// create, but breaks specifically inside a transaction Bundle on HAPI v8.0.0 when the
    /// same entry also carries an unresolved forward reference to another entry in the same
    /// transaction (HAPI-0389 "Invalid match URL format") — found by isolating it with raw
    /// curl against local HAPI. Giving Consent its own identifier sidesteps the bug rather
    /// than working around it blind.
    /// </param>
    public static Consent Build(string patientUrn, string nationalId) => new()
    {
        Status = Consent.ConsentState.Active,
        Identifier = [new Identifier(Systems.NationalId, nationalId)],
        Scope = new CodeableConcept("http://terminology.hl7.org/CodeSystem/consentscope", "patient-privacy"),
        // Not consentcategorycodes (that system is DNR/advance-directive/research-style
        // categories, no general "information access" entry) — v3-ActCode#INFA is the
        // real code for that, confirmed against tx.fhir.org's own $lookup after HAPI's
        // request-time validation rejected a plausible-looking but nonexistent code here
        // (consentcategorycodes#INFAO) on first submit. Same category of finding as
        // Phase N's malaria codes: verify against a live terminology server, don't guess.
        Category = [new CodeableConcept("http://terminology.hl7.org/CodeSystem/v3-ActCode", "INFA", "information access")],
        Patient = new ResourceReference(patientUrn),
        DateTime = DateTimeOffset.UtcNow.UtcDateTime.ToString("o"),
        Policy = [new Consent.PolicyComponent { Uri = PolicyRule }],
        Provision = new Consent.provisionComponent { Type = Consent.ConsentProvisionType.Permit },
    };
}
