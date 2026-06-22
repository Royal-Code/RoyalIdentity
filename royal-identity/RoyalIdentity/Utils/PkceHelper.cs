using RoyalIdentity.Security.Cryptography;
using SecurityBase64Url = RoyalIdentity.Security.Encoding.Base64Url;
using System.Text;

namespace RoyalIdentity.Utils;

public static class PkceHelper
{
    public static string GenerateCodeChallengeS256(string codeVerifier)
    {
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var transformedCodeVerifier = SecurityBase64Url.Encode(Hashing.Sha256(codeVerifierBytes));
        return Hashing.Sha256Base64(transformedCodeVerifier);
    }
}
