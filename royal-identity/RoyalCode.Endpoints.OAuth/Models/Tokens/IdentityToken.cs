namespace RoyalIdentity.Models.Tokens;

public class IdentityToken : TokenBase
{
    public IdentityToken(
        string clientId,
        string issuer,
        DateTime creationTime,
        int lifetime) : base(clientId, issuer, creationTime, lifetime)
    { }
}
