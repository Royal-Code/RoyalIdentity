using RoyalIdentity.Extensions;
using System.Text;

namespace RoyalIdentity.Utils;

public static class PkceHelper
{
    public static string GenerateCodeChallengeS256(string codeVerifier)
    {
        var codeVerifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var transformedCodeVerifier = Base64Url.Encode(codeVerifierBytes.Sha256());
        return transformedCodeVerifier.Sha256();
    }
}
