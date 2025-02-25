﻿using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace RoyalIdentity.Utils;

/// <summary>
/// Crypto helper
/// </summary>
public static class CryptoHelper
{
    /// <summary>
    /// Creates the hash for the various hash claims (e.g. c_hash, at_hash or s_hash).
    /// </summary>
    /// <param name="value">The value to hash.</param>
    /// <param name="tokenSigningAlgorithm">The token signing algorithm</param>
    /// <returns></returns>
    public static string CreateHashClaimValue(string value, string tokenSigningAlgorithm)
    {
        using var sha = GetHashAlgorithmForSigningAlgorithm(tokenSigningAlgorithm);
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(value));
        var size = sha.HashSize / 8 / 2;

        var leftPart = new byte[size];
        Array.Copy(hash, leftPart, size);

        return Base64Url.Encode(leftPart);
    }

    /// <summary>
    /// Returns the matching hashing algorithm for a token signing algorithm
    /// </summary>
    /// <param name="signingAlgorithm">The signing algorithm</param>
    /// <returns></returns>
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
    internal static ECCurve GetCurveFromCrvValue(string crv)
    {
        return crv switch
        {
            JsonWebKeyECTypes.P256 => ECCurve.NamedCurves.nistP256,
            JsonWebKeyECTypes.P384 => ECCurve.NamedCurves.nistP384,
            JsonWebKeyECTypes.P521 => ECCurve.NamedCurves.nistP521,
            _ => throw new InvalidOperationException($"Unsupported curve type of {crv}"),
        };
    }
}
