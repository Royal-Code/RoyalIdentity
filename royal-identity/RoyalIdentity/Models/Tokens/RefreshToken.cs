using RoyalIdentity.Options;
using System.Security.Claims;

namespace RoyalIdentity.Models.Tokens;

public class RefreshToken: TokenBase
{
    public RefreshToken(
        string subjectId,
        string sessionId,
        ICollection<string> requestedScopes,
        string clientId,
        string issuer,
        DateTime creationTime,
        int lifetime,
        string tokenItSelf) : base(clientId, issuer, creationTime, lifetime, tokenItSelf)
    {
        Claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subjectId));
        Claims.Add(new Claim(JwtRegisteredClaimNames.Sid, sessionId));

        RequestedScopes = requestedScopes;
    }

    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public ICollection<string> RequestedScopes { get; }

    /// <summary>
    /// <para>
    ///     Where the claims of a token renewed with this refresh token come from
    ///     (plan-data-operational-storage DF32). It is captured when the token is issued, from the realm policy
    ///     in force at that moment, so changing <c>RealmOptions.RefreshTokens.ClaimsMode</c> later never
    ///     reinterprets tokens that already exist.
    /// </para>
    /// <para>
    ///     <see cref="RefreshTokenClaimsMode.Current"/> keeps only the minimal grant here and re-runs issuance
    ///     against the current claims; <see cref="RefreshTokenClaimsMode.Snapshot"/> keeps in
    ///     <see cref="TokenBase.Claims"/> everything needed to reproduce the emitted claims. Neither depends on
    ///     the row of the access token that was issued alongside it (DF41).
    /// </para>
    /// </summary>
    public RefreshTokenClaimsMode ClaimsMode { get; set; } = RefreshTokenClaimsMode.Current;

    /// <summary>
    /// RFC 8707 protected resource URIs authorized for refresh-token renewal.
    /// </summary>
    public ICollection<string> ResourceUris { get; } = [];

    /// <summary>
    /// Gets or sets the consumed time.
    /// </summary>
    /// <value>
    /// The consumed time.
    /// </value>
    public DateTime? ConsumedTime { get; set; }

    /// <summary>
    /// <para>
    ///     State version of the persisted token, owned by the store: materialization publishes the version the
    ///     row had, and the conditional transitions of <see cref="Contracts.Storage.IVersionedRefreshTokenStore"/>
    ///     use it as the expected value (plan-data-operational-storage DF12).
    /// </para>
    /// <para>
    ///     A caller must never build the expected value from the same instance it already mutated: pass the
    ///     version obtained from materialization, so a lost race is observable instead of trivially winning.
    /// </para>
    /// </summary>
    public int StateVersion { get; set; }
}
