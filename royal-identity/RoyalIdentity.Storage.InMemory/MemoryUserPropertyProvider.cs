using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using static RoyalIdentity.Options.Constants;

namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// In-memory (fake/reference) <see cref="IUserPropertyProvider"/>: projects the account's properties into
/// claim DTOs filtered by the requested claim types. Receives only primitives (ADR-014 §2.9) — it never
/// sees rich IdP types. Realm is bound at construction.
/// <para>
/// The fake's record is a flat claim/role bag, so projection is driven entirely by <c>claimTypes</c>; the
/// <c>identityScopeNames</c> argument is unused here (a real UsersAccounts module, with properties
/// partitioned per scope, would honor it). Roles are emitted as <c>role</c> claims through this path, never
/// via the minimal session principal (ADR-014 §2.8).
/// </para>
/// </summary>
public sealed class MemoryUserPropertyProvider(ConcurrentDictionary<string, MemoryUserAccount> users) : IUserPropertyProvider
{
    public Task<IReadOnlyList<UserClaimDto>> GetClaimsAsync(
        string subjectId,
        IReadOnlyCollection<string> identityScopeNames,
        IReadOnlyCollection<string> claimTypes,
        CancellationToken ct = default)
    {
        var details = users.Values.FirstOrDefault(u => u.SubjectId == subjectId);
        if (details is null || !details.IsActive)
            return Task.FromResult<IReadOnlyList<UserClaimDto>>([]);

        var all = new List<UserClaimDto>
        {
            new(JwtRegisteredClaimNames.Name, details.DisplayName),
            new(Jwt.ClaimTypes.PreferredUserName, details.DisplayName),
        };
        all.AddRange(details.Claims.Select(c => new UserClaimDto(c.Type, c.Value, c.ValueType)));
        all.AddRange(details.Roles.Select(r => new UserClaimDto(Jwt.ClaimTypes.Role, r)));

        // Strict projection by the requested claim types: a property is emitted only when its type was
        // requested through an identity scope. No claim types requested ⇒ no profile claims (matches the
        // previous DefaultProfileService behavior; an API-only access token must not leak profile claims).
        IReadOnlyList<UserClaimDto> projected = [.. all.Where(c => claimTypes.Contains(c.Type))];

        return Task.FromResult(projected);
    }
}
