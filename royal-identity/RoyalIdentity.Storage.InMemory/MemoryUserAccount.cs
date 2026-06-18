using System.Security.Claims;

namespace RoyalIdentity.Storage.InMemory;

#nullable disable // POCO
#pragma warning disable // POCO

/// <summary>
/// In-memory (fake/reference) account record backing the edge facades (<c>ISubjectStore</c> /
/// <c>ILocalUserAuthenticator</c> / <c>IUserClaimsProvider</c>). This is <b>not</b> a core contract: the
/// borda is 100% facade and speaks only <c>Subject</c> + primitives; the rich account model lives in the
/// future RoyalIdentity.UserAccounts module (ADR-013/014/015). Kept here only as the fake store's persistence
/// shape (failure counters and claims are mutated in place).
/// </summary>
public class MemoryUserAccount
{
    /// <summary>
    /// Stable, immutable identifier — the OIDC <c>sub</c>. Separate from <see cref="Username"/> and never
    /// derived from it (ADR-014 §2.2). Changing the username must not change this value.
    /// </summary>
    public string SubjectId { get; set; }

    public string Username { get; set; }

    public string? PasswordHash { get; set; }

    public string DisplayName { get; set; }

    public bool IsActive { get; set; }

    public int LoginAttemptsWithPasswordErrors { get; set; }

    public DateTimeOffset? LastPasswordError { get; set; }

    public HashSet<string> Roles { get; set; } = [];

    public HashSet<Claim> Claims { get; set; } = [];
}
