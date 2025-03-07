using System.Security.Claims;

namespace RoyalIdentity.Users.Contracts;

#nullable disable // POCO
#pragma warning disable // POCO

public class UserDetails
{
    public string Username { get; set; }

    public string? PasswordHash { get; set; }

    public string DisplayName { get; set; }

    public bool IsActive { get; set; }

    public int LoginAttemptsWithPasswordErrors { get; set; }

    public DateTimeOffset? LastPasswordError { get; set; }

    public HashSet<string> Roles { get; set; } = [];

    public HashSet<Claim> Claims { get; set; } = [];
}