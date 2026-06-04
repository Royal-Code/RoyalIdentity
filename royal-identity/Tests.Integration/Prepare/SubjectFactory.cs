using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

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
            new(Jwt.ClaimTypes.Role, role),
            new(JwtClaimTypes.SessionId, CryptoRandom.CreateUniqueId(24)),
            new(JwtClaimTypes.AuthenticationTime, DateTimeOffset.Now.ToUnixTimeSeconds().ToString()),
            new(Jwt.ClaimTypes.IdentityProvider, ServerConstants.LocalIdentityProvider),
            new(JwtClaimTypes.AuthenticationMethod, AuthenticationMethods.Password)
        ];

        var identity = new ClaimsIdentity(claims, "tests", JwtClaimTypes.Subject, Jwt.ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
