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
            new(JwtRegisteredClaimNames.Sub, sub),
            new(JwtClaimTypes.Name, name),
            new(Jwt.ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Sid, CryptoRandom.CreateUniqueId(24)),
            new(JwtRegisteredClaimNames.AuthTime, DateTimeOffset.Now.ToUnixTimeSeconds().ToString()),
            new(Jwt.ClaimTypes.IdentityProvider, ServerConstants.LocalIdentityProvider),
            new(JwtRegisteredClaimNames.Amr, AuthenticationMethods.Password)
        ];

        var identity = new ClaimsIdentity(claims, "tests", JwtRegisteredClaimNames.Sub, Jwt.ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
