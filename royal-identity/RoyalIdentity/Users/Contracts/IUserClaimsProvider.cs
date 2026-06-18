using System.Security.Claims;

namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Projects user properties into claims for the requested identity scopes. Receives ONLY primitives
/// (identity scope names + claim types) so the accounts module never sees rich IdP types
/// (<c>IdentityScope</c> / <c>RequestedResources</c>) — ADR-014 §2.9/§4. Realm is bound at construction.
/// <para>
/// Returns <see cref="Claim"/> directly (BCL type): the immediate consumer (the IdP) already issues
/// <c>Claim</c> for token/userinfo, so no intermediate DTO is needed — the decoupling comes from the
/// primitive <b>parameters</b>, not from the return type (ADR-014 §4 / ADR-015 §2.4).
/// </para>
/// </summary>
public interface IUserClaimsProvider
{
    /// <summary>
    /// Returns the claims for the subject, limited to the requested identity scope names and claim types.
    /// </summary>
    Task<IReadOnlyList<Claim>> GetClaimsAsync(
        string subjectId,
        IReadOnlyCollection<string> identityScopeNames,
        IReadOnlyCollection<string> claimTypes,
        CancellationToken ct = default);
}
