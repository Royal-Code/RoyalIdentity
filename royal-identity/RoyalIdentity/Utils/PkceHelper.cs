using RoyalIdentity.Security.Cryptography;
using SecurityBase64Url = RoyalIdentity.Security.Encoding.Base64Url;
using System.Text;

namespace RoyalIdentity.Utils;

public static class PkceHelper
{
    public static string GenerateS256CodeChallenge(string codeVerifier)
    {
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        return SecurityBase64Url.Encode(Hashing.Sha256(codeVerifierBytes));
    }

    public static string GenerateStoredS256CodeChallengeHash(string codeVerifier)
    {
        return HashCodeChallengeForStorage(GenerateS256CodeChallenge(codeVerifier));
    }

    public static string HashCodeChallengeForStorage(string? codeChallenge)
    {
        if (string.IsNullOrWhiteSpace(codeChallenge))
            return string.Empty;

        return Hashing.Sha256Base64(codeChallenge);
    }

    [Obsolete("Use GenerateStoredS256CodeChallengeHash for persisted authorization code comparisons.")]
    public static string GenerateCodeChallengeS256(string codeVerifier)
    {
        return GenerateStoredS256CodeChallengeHash(codeVerifier);
    }
}
