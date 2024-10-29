using System.Security.Claims;

namespace RoyalIdentity.Users.Contracts;

public class UserDetails
{
    public string UserName { get; set; }

    public string PasswordHash { get; set; }

    public string DisplayName { get; set; }

    public bool IsActive { get; set; }

    public int LoginAttemptsWithPasswordErrors { get; set; }

    public HashSet<Claim> Claims { get; } = [];
}