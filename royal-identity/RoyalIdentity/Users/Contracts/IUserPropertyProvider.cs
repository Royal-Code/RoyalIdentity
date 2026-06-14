namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Projects user properties into claims for the requested identity scopes. Receives ONLY primitives
/// (identity scope names + claim types) so the accounts module never sees rich IdP types
/// (<c>IdentityScope</c> / <c>RequestedResources</c>) — ADR-014 §2.9. Realm is bound at construction.
/// </summary>
public interface IUserPropertyProvider
{
    /// <summary>
    /// Returns the claims for the subject, limited to the requested identity scope names and claim types.
    /// </summary>
    Task<IReadOnlyList<UserClaimDto>> GetClaimsAsync(
        string subjectId,
        IReadOnlyCollection<string> identityScopeNames,
        IReadOnlyCollection<string> claimTypes,
        CancellationToken ct = default);
}
