using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;

namespace RoyalIdentity.Models.Tokens;

public abstract class TokenBase
{
    protected TokenBase(
        string clientId,
        string issuer,
        AccessTokenType accessTokenType,
        DateTime creationTime,
        int lifetime)
    {
        ClientId = clientId;
        Issuer = issuer;
        AccessTokenType = accessTokenType;
        CreationTime = creationTime;
        Lifetime = lifetime;
    }

    /// <summary>
    /// Gets or sets the ID of the client.
    /// </summary>
    /// <value>
    /// The ID of the client.
    /// </value>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the issuer.
    /// </summary>
    /// <value>
    /// The issuer.
    /// </value>
    public string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the type of access token of the client (JWT or Reference)
    /// </summary>
    /// <value>
    /// The access token type specified by the client.
    /// </value>
    public AccessTokenType AccessTokenType { get; set; }

    /// <summary>
    /// Gets or sets the creation time.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the lifetime.
    /// </summary>
    /// <value>
    /// The lifetime.
    /// </value>
    public int Lifetime { get; set; }

    /// <summary>
    /// Gets or sets the claims.
    /// </summary>
    /// <value>
    /// The claims.
    /// </value>
    public HashSet<Claim> Claims { get; set; } = new HashSet<Claim>(new ClaimComparer());

    /// <summary>
    /// A list of allowed algorithm for signing the token. If null or empty, will use the default algorithm.
    /// </summary>
    public HashSet<string> AllowedSigningAlgorithms { get; set; } = [];

    /// <summary>
    /// Specifies the confirmation method of the token. This value, if set, will become the cnf claim.
    /// </summary>
    public string? Confirmation { get; set; }

    /// <summary>
    /// Gets or sets the audiences.
    /// </summary>
    /// <value>
    /// The audiences.
    /// </value>
    public HashSet<string> Audiences { get; set; } = [];

    /// <summary>
    /// Gets the subject identifier.
    /// </summary>
    /// <value>
    /// The subject identifier.
    /// </value>
    public string? SubjectId => Claims.Where(x => x.Type == JwtClaimTypes.Subject).Select(x => x.Value).SingleOrDefault();

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string? SessionId => Claims.Where(x => x.Type == JwtClaimTypes.SessionId).Select(x => x.Value).SingleOrDefault();

    /// <summary>
    /// Gets the scopes.
    /// </summary>
    /// <value>
    /// The scopes.
    /// </value>
    public IEnumerable<string> Scopes => Claims.Where(x => x.Type == JwtClaimTypes.Scope).Select(x => x.Value);
}
