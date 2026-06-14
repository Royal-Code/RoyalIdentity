using System.Security.Claims;

namespace RoyalIdentity.Users.Contracts;

#nullable disable // POCO
#pragma warning disable // POCO

public class UserDetails
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