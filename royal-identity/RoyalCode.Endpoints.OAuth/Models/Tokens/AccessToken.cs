
namespace RoyalIdentity.Models.Tokens;

public class AccessToken : TokenBase
{
    public AccessToken(
        string clientId,
        string issuer,
        AccessTokenType accessTokenType,
        DateTime creationTime,
        int lifetime,
        string token,
        string tokenType) : base(clientId, issuer, accessTokenType, creationTime, lifetime)
    {
        Token = token;
        TokenType = tokenType;
    }

    public string Token { get; set; }

    public string TokenType { get; set; }
}
