using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Security.Cryptography;
using System.Security.Cryptography;

namespace RoyalIdentity.Utils;

/// <summary>
/// Crypto helper
/// </summary>
public static class CryptoHelper
{
    /// <summary>
    /// Creates the hash for the various hash claims (e.g. c_hash, at_hash or s_hash).
    /// </summary>
    public static string CreateHashClaimValue(string value, string tokenSigningAlgorithm)
    {
        var bits = int.Parse(tokenSigningAlgorithm[^3..]);
        var algorithm = bits switch
        {
            256 => HashAlgorithmName.SHA256,
            384 => HashAlgorithmName.SHA384,
            512 => HashAlgorithmName.SHA512,
            _ => throw new InvalidOperationException($"Invalid signing algorithm: {tokenSigningAlgorithm}"),
        };

        return Hashing.LeftHalfHashBase64Url(value, algorithm);
    }

    /// <summary>
    /// Returns the matching hashing algorithm for a token signing algorithm
    /// </summary>
    public static HashAlgorithm GetHashAlgorithmForSigningAlgorithm(string signingAlgorithm)
    {
        var signingAlgorithmBits = int.Parse(signingAlgorithm[^3..]);

        return signingAlgorithmBits switch
        {
            256 => SHA256.Create(),
            384 => SHA384.Create(),
            512 => SHA512.Create(),
            _ => throw new InvalidOperationException($"Invalid signing algorithm: {signingAlgorithm}"),
        };
    }

    /// <summary>
    /// Returns the matching named curve for RFC 7518 crv value
    /// </summary>
    internal static System.Security.Cryptography.ECCurve GetCurveFromCrvValue(string crv)
    {
        return crv switch
        {
            JsonWebKeyECTypes.P256 => System.Security.Cryptography.ECCurve.NamedCurves.nistP256,
            JsonWebKeyECTypes.P384 => System.Security.Cryptography.ECCurve.NamedCurves.nistP384,
            JsonWebKeyECTypes.P521 => System.Security.Cryptography.ECCurve.NamedCurves.nistP521,
            _ => throw new InvalidOperationException($"Unsupported curve type of {crv}"),
        };
    }
}
