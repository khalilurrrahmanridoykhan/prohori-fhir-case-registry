using System.ComponentModel.DataAnnotations;

namespace Prohori.Api.Models;

public enum ConsentDecision { Permit, Deny }

/// <summary>Body of <c>PUT /patients/{nationalId}/consent</c>.</summary>
public sealed record ConsentToggleRequest
{
    [Required] public ConsentDecision Provision { get; init; }
}
