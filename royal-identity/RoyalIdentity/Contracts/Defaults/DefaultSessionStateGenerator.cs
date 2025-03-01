// Ignore Spelling: Opuas

using RoyalIdentity.Contexts;
using RoyalIdentity.Utils;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultSessionStateGenerator : ISessionStateGenerator
{
    public string GenerateSessionStateValue(AuthorizeContext context)
    {
        context.AssertHasRedirectUri();

        var principal = context.Subject;
        var clientId = context.ClientId;
        var sessionId = context.SessionId!;
        var uri = new Uri(context.RedirectUri);
        var origin = uri.Scheme + "://" + uri.Host;

        if (!uri.IsDefaultPort)
            origin += ":" + uri.Port;

        var opuas = GenerateOpuas(sessionId, principal);
        var salt = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex);

        var bytes = Encoding.UTF8.GetBytes(clientId + origin + opuas + salt);
        byte[] hash;
        hash = SHA256.HashData(bytes);

        return Base64Url.Encode(hash) + "." + salt;
    }

    /// <summary>
    /// Generate OpenID Provider User Agent State
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static string GenerateOpuas(string sessionId, ClaimsPrincipal principal)
    {
        var claims = string.Join("", principal.Claims.Select(c => $"{c.Type}{c.Value}"));
        if (string.IsNullOrEmpty(claims))
            return sessionId;

        var bytes = Encoding.UTF8.GetBytes(sessionId + claims);
        var hash = SHA256.HashData(bytes)!;

        return Base64Url.Encode(hash);
    }
}

