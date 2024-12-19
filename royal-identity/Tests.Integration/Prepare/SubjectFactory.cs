using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;

namespace Tests.Integration.Prepare;

/// <summary>
/// A factory for creating subjects.
/// </summary>
public static class SubjectFactory
{

    public static ClaimsPrincipal Create(string sub, string name, string role)
    {
        Claim[] claims = 
        [
            new(JwtClaimTypes.Subject, sub),
            new(JwtClaimTypes.Name, name),
            new(JwtClaimTypes.Role, role),
            new(JwtClaimTypes.SessionId, CryptoRandom.CreateUniqueId(24)),
            new(JwtClaimTypes.AuthenticationTime, DateTimeOffset.Now.ToUnixTimeSeconds().ToString()),
            new(JwtClaimTypes.IdentityProvider, ServerConstants.LocalIdentityProvider),
            new(JwtClaimTypes.AuthenticationMethod, "password")
        ];

        var identity = new ClaimsIdentity(claims, "tests", JwtClaimTypes.Subject, JwtClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
