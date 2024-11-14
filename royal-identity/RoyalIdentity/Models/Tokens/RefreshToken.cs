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
        Claims.Add(new Claim(JwtClaimTypes.Subject, subjectId));
        Claims.Add(new Claim(JwtClaimTypes.SessionId, sessionId));
        Claims.Add(new Claim(JwtClaimTypes.JwtId, accessTokenId));

        RequestedScopes = requestedScopes;
    }

    /// <summary>
    /// Gets the access token id (jit).
    /// </summary>
    public string? AccessTokenId => Claims.Where(x => x.Type == JwtClaimTypes.JwtId).Select(x => x.Value).SingleOrDefault();

    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public ICollection<string> RequestedScopes { get; }

    /// <summary>
    /// Gets or sets the consumed time.
    /// </summary>
    /// <value>
    /// The consumed time.
    /// </value>
    public DateTime? ConsumedTime { get; set; }
}