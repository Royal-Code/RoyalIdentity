namespace RoyalIdentity.Users;

/// <summary>
/// Core-owned representation of which of a subject's sessions/tokens to revoke. The module's per-trigger policy
/// (<c>SessionInvalidation</c>) is translated to this by the integration; the core never references the module enum
/// (ADR-017 §2.7/§2.10, Q13).
/// </summary>
[Flags]
public enum SessionRevocation
{
    /// <summary>No revocation.</summary>
    None = 0,

    /// <summary>The current session (the one that triggered the change).</summary>
    CurrentSession = 1,

    /// <summary>All sessions except the current one.</summary>
    OtherSessions = 2,

    /// <summary>All interactive sessions (current and others).</summary>
    AllSessions = CurrentSession | OtherSessions,

    /// <summary>The subject's refresh tokens.</summary>
    RefreshTokens = 4
}
