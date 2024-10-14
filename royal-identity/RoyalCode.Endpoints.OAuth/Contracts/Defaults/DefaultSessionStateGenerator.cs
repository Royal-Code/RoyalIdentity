using RoyalIdentity.Contexts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Utils;
using System.Security.Cryptography;
using System.Text;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultSessionStateGenerator : ISessionStateGenerator
{
    [Redesign("É necessáio tem opções, onde não se deve usar apenas o SessionId para gerar o hash")]
    [Redesign("Mas deve utilizar quase todos claims, removendo os que são de tempo e relativos ao protocolo")]
    public string? GenerateSessionStateValue(AuthorizeContext context)
    {
        if (!context.IsOpenIdRequest || context.SessionId.IsMissing() ||
            context.ClientId.IsMissing() || context.RedirectUri.IsMissing())
        {
            return null;
        }

        var clientId = context.ClientId;
        var sessionId = context.SessionId;
        var salt = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex);

        var uri = new Uri(context.RedirectUri);
        var origin = uri.Scheme + "://" + uri.Host;
        if (!uri.IsDefaultPort)
        {
            origin += ":" + uri.Port;
        }

        var bytes = Encoding.UTF8.GetBytes(clientId + origin + sessionId + salt);
        byte[] hash;

        hash = SHA256.HashData(bytes);

        return Base64Url.Encode(hash) + "." + salt;
    }
}

