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
            new(JwtRegisteredClaimNames.Sub, sub),
            new(JwtRegisteredClaimNames.Name, name),
            new(Jwt.ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Sid, CryptoRandom.CreateUniqueId(24)),
            new(JwtRegisteredClaimNames.AuthTime, DateTimeOffset.Now.ToUnixTimeSeconds().ToString()),
            new(Jwt.ClaimTypes.IdentityProvider, Server.LocalIdentityProvider),
            new(JwtRegisteredClaimNames.Amr, Oidc.AuthMethods.Password)
        ];

        var identity = new ClaimsIdentity(claims, "tests", JwtRegisteredClaimNames.Sub, Jwt.ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
