namespace RoyalIdentity.Models.Security;

/// <summary>
/// Describes the configuration status derived by a client security assessment.
/// </summary>
public enum SecurityAssessmentStatus
{
    /// <summary>No applicable configuration finding was produced.</summary>
    Compliant = 0,

    /// <summary>One or more recommended practices are not satisfied.</summary>
    Warning = 1,

    /// <summary>One or more mandatory requirements are not satisfied.</summary>
    NonCompliant = 2,
}
