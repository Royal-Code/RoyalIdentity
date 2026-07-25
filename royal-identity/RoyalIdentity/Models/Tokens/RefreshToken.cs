using RoyalIdentity.Options;
using System.Security.Claims;

namespace RoyalIdentity.Models.Tokens;

public class RefreshToken: TokenBase
{
    public RefreshToken(
        string subjectId,
        string sessionId,
        string accessTokenId,
        ICollection<string> requestedScopes,
        string clientId,
        string issuer,
        DateTime creationTime,
        int lifetime,
        string tokenItSelf) : base(clientId, issuer, creationTime, lifetime, tokenItSelf)
    {
        Claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subjectId));
        Claims.Add(new Claim(JwtRegisteredClaimNames.Sid, sessionId));
        Claims.Add(new Claim(JwtRegisteredClaimNames.Jti, accessTokenId));

        RequestedScopes = requestedScopes;
    }

    /// <summary>
    /// Gets the access token id (jit).
    /// </summary>
    public string? AccessTokenId => Claims.Where(x => x.Type == JwtRegisteredClaimNames.Jti).Select(x => x.Value).SingleOrDefault();

    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public ICollection<string> RequestedScopes { get; }

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
