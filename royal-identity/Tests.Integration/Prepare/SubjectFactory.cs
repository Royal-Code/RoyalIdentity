using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Security.Cryptography;
using System.Security.Claims;

namespace Tests.Integration.Prepare;

/// <summary>
/// A factory for creating subjects.
/// </summary>
public static class SubjectFactory
{
    /// <summary>
    /// Builds a synthetic principal (random <c>sid</c>) <b>without</b> a backing session. Use only where the
    /// unified "active" rule is not exercised (e.g. consent or store-isolation tests). For token-endpoint
    /// tests that exchange a code/refresh token, use <see cref="CreateWithSession"/>.
    /// </summary>
    public static ClaimsPrincipal Create(string sub, string name, string role)
        => Build(sub, name, role, CryptoRandom.CreateUniqueId(24));

    /// <summary>
    /// Builds a principal AND seeds a matching ACTIVE session in the realm store, so the unified "active"
    /// rule (account active + valid session, ADR-014 §2.7) holds for synthetic token-endpoint tests. Pass a
    /// real seed SubjectId (e.g. <c>MemoryStorage.AliceSubjectId</c>) so the account lookup resolves.
    /// </summary>
    public static ClaimsPrincipal CreateWithSession(
        IStorage storage, RoyalIdentity.Models.Realm realm, string subjectId, string name, string role)
    {
        var sid = CryptoRandom.CreateUniqueId(24);
        storage.GetUserSessionStore(realm).CreateAsync(new UserSession
        {
            Id = sid,
            SubjectId = subjectId,
            AuthenticationMethod = Oidc.AuthMethods.Password,
            IdentityProvider = Server.LocalIdentityProvider,
            StartedAt = DateTime.UtcNow,
        }).GetAwaiter().GetResult();

        return Build(subjectId, name, role, sid);
    }

    private static ClaimsPrincipal Build(string sub, string name, string role, string sid)
    {
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, sub),
            new(JwtRegisteredClaimNames.Name, name),
            new(Jwt.ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Sid, sid),
            new(JwtRegisteredClaimNames.AuthTime, DateTimeOffset.Now.ToUnixTimeSeconds().ToString()),
            new(Jwt.ClaimTypes.IdentityProvider, Server.LocalIdentityProvider),
            new(JwtRegisteredClaimNames.Amr, Oidc.AuthMethods.Password)
        ];

        var identity = new ClaimsIdentity(claims, "tests", JwtRegisteredClaimNames.Sub, Jwt.ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
