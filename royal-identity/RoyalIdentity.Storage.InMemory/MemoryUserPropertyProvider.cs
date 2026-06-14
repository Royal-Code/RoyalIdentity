using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
using static RoyalIdentity.Options.Constants;

namespace RoyalIdentity.Storage.InMemory;

/// <summary>
/// In-memory (fake/reference) <see cref="IUserPropertyProvider"/>: projects the account's properties into
/// claim DTOs filtered by the requested claim types. Receives only primitives (ADR-014 §2.9) — it never
/// sees rich IdP types. Wired into the profile/claims path in Fase 8; in Fase 4 it is a functional stub.
/// Realm is bound at construction.
/// </summary>
public sealed class MemoryUserPropertyProvider(ConcurrentDictionary<string, UserDetails> users) : IUserPropertyProvider
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

        IReadOnlyList<UserClaimDto> projected = claimTypes.Count == 0
            ? all
            : all.Where(c => claimTypes.Contains(c.Type)).ToList();

        return Task.FromResult(projected);
    }
}
