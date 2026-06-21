using System.Security.Claims;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;
using RoyalIdentity.UserAccounts.Options;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Module-backed <see cref="IUserClaimsProvider"/> (ADR-015 §2.1): projects the account's fixed fields, roles
/// and dynamic scope properties into BCL <see cref="Claim"/> instances. The realm and its fixed-field
/// projections are bound at construction.
/// <para>
/// The requested identity scope names and claim types are treated as an <b>intersection</b> filter, never as an
/// authorization grant: a claim is emitted only when it exists in the module for a requested scope AND its type
/// was requested by the IdP. An inactive or missing account yields no claims. The BCL <see cref="Claim"/> type
/// appears only here at the edge — the pure module speaks internal <see cref="AccountClaimValue"/>.
/// </para>
/// </summary>
public sealed class UserClaimsProvider(
    UserAccountClaimsReader reader,
    string realmId,
    UserAccountsRealmOptions options) : IUserClaimsProvider
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Claim>> GetClaimsAsync(
        string subjectId,
        IReadOnlyCollection<string> identityScopeNames,
        IReadOnlyCollection<string> claimTypes,
        CancellationToken ct = default)
    {
        var values = await reader.GetClaimsAsync(realmId, subjectId, options, identityScopeNames, claimTypes, ct);
        return [.. values.Select(v => new Claim(v.ClaimType, v.Value))];
    }
}
