namespace RoyalIdentity.Models.Security;

/// <summary>
/// Identifies the normative requirement level associated with a security finding.
/// </summary>
public enum SecurityRequirementLevel
{
    /// <summary>The governing specification says MUST.</summary>
    Must = 0,

    /// <summary>The governing specification says MUST NOT.</summary>
    MustNot = 1,

    /// <summary>The governing specification says SHOULD.</summary>
    Should = 2,

    /// <summary>The governing specification says SHOULD NOT.</summary>
    ShouldNot = 3,

    /// <summary>The governing specification explicitly recommends the practice.</summary>
    Recommended = 4,
}
