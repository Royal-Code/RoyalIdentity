namespace RoyalIdentity.Models.Security;

/// <summary>
/// An immutable technical finding produced from observable client and realm configuration.
/// </summary>
public sealed record ClientSecurityFinding
{
    internal ClientSecurityFinding(
        string ruleId,
        SecurityRequirementLevel requirementLevel,
        SecurityAssessmentStatus status,
        SecurityFindingSeverity severity,
        string description,
        string remediation)
    {
        if (status is SecurityAssessmentStatus.Compliant)
            throw new ArgumentOutOfRangeException(nameof(status), "A compliant rule does not produce a finding.");

        RuleId = ruleId;
        RequirementLevel = requirementLevel;
        Status = status;
        Severity = severity;
        Description = description;
        Remediation = remediation;
    }

    /// <summary>Gets the stable, non-localized rule identifier.</summary>
    public string RuleId { get; }

    /// <summary>Gets the normative requirement level.</summary>
    public SecurityRequirementLevel RequirementLevel { get; }

    /// <summary>Gets the status contributed to the containing assessment.</summary>
    public SecurityAssessmentStatus Status { get; }

    /// <summary>Gets the technical remediation priority.</summary>
    public SecurityFindingSeverity Severity { get; }

    /// <summary>Gets the non-localized technical description.</summary>
    public string Description { get; }

    /// <summary>Gets the non-localized technical remediation guidance.</summary>
    public string Remediation { get; }
}
