namespace RoyalIdentity.Models.Security;

/// <summary>
/// Describes the technical remediation priority of a client security finding.
/// </summary>
public enum SecurityFindingSeverity
{
    /// <summary>Low-priority hardening opportunity.</summary>
    Low = 0,

    /// <summary>Material hardening recommendation.</summary>
    Medium = 1,

    /// <summary>High-impact security weakness.</summary>
    High = 2,

    /// <summary>Configuration can directly expose credentials or protocol artifacts.</summary>
    Critical = 3,
}
