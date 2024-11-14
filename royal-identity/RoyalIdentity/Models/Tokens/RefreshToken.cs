namespace RoyalIdentity.Models.Tokens;

public class RefreshToken: TokenBase
{
    public RefreshToken(string clientId,
        string issuer,
        DateTime creationTime,
        int lifetime,
        string? tokenItSelf = null) : base(clientId,
        issuer,
        creationTime,
        lifetime,
        tokenItSelf)
    {
    }
}