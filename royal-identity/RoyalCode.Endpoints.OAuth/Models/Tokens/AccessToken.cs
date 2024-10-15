
namespace RoyalIdentity.Models.Tokens;

public class AccessToken : TokenBase
{
    public AccessToken(
        string clientId,
        string issuer,
        AccessTokenType accessTokenType,
        DateTime creationTime,
        int lifetime) : base(clientId, issuer, accessTokenType, creationTime, lifetime)
    {
    }
}
