
namespace RoyalIdentity.Models.Tokens;

public class AccessToken : TokenBase
{
    public AccessToken(
        string clientId,
        string issuer,
        AccessTokenType accessTokenType,
        DateTime creationTime,
        int lifetime,
        string jti,
        string tokenType) : base(clientId, issuer, creationTime, lifetime, jti)
    {
        Id = jti;
        TokenType = tokenType;
        AccessTokenType = accessTokenType;
    }

    /// <summary>
    /// Token Identity or JTI.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets or sets the type of access token of the client (JWT or Reference)
    /// </summary>
    /// <value>
    /// The access token type specified by the client.
    /// </value>
    public AccessTokenType AccessTokenType { get; }

    public string TokenType { get; set; }
}
