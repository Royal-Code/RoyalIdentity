
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Security.Claims;

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

    /// <summary>
    /// The type of the token, for example "Bearer".
    /// </summary>
    public string TokenType { get; set; }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    /// <returns></returns>
    public AccessToken Renew(string jti, DateTime creationTime, int lifetime)
    {
        var newToken = new AccessToken(
            ClientId,
            Issuer,
            AccessTokenType,
            creationTime,
            lifetime,
            jti,
            TokenType)
        {
            AllowedSigningAlgorithms = AllowedSigningAlgorithms,
            Confirmation = Confirmation,
            Audiences = Audiences,
        };

        newToken.Claims.AddRange(Claims.Where(c => c.Type != "jti"));
        newToken.Claims.Add(new Claim(JwtClaimTypes.JwtId, jti));

        return newToken;
    }
}
