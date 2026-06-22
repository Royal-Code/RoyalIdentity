using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace RoyalIdentity.Security.Keys;

/// <summary>
/// Generates fresh <see cref="KeyParameters"/> for a signing algorithm. This is the generic,
/// IdP-agnostic counterpart of the core's <c>KeyOptions</c>-aware factory: it takes the algorithm,
/// lifetime and RSA key size as plain arguments instead of reading them from realm options.
/// </summary>
/// <remarks>
/// The IdP core keeps its own factory that validates the algorithm against the realm
/// <c>KeyOptions</c> and then delegates here, preserving realm policy in the core.
/// </remarks>
public static class KeyMaterialFactory
{
    /// <summary>Default RSA key size, in bits, when none is provided.</summary>
    public const int DefaultRsaKeySizeInBits = 2048;

    /// <summary>
    /// Creates new <see cref="KeyParameters"/> with freshly generated material for the given algorithm.
    /// </summary>
    /// <param name="algorithm">Security algorithm, see <see cref="SecurityAlgorithms"/>.</param>
    /// <param name="lifetime">
    /// Optional key lifetime. When provided, <see cref="KeyParameters.NotBefore"/> is set to the creation
    /// time and <see cref="KeyParameters.Expires"/> to creation time plus <paramref name="lifetime"/>.
    /// </param>
    /// <param name="rsaKeySizeInBits">RSA key size in bits; only used for RSA/PSS algorithms.</param>
    /// <returns>A new <see cref="KeyParameters"/> instance.</returns>
    /// <exception cref="NotSupportedException">The algorithm is not recognized or supported.</exception>
    public static KeyParameters Create(
        string algorithm,
        TimeSpan? lifetime = null,
        int rsaKeySizeInBits = DefaultRsaKeySizeInBits)
    {
        ArgumentException.ThrowIfNullOrEmpty(algorithm);

        string keyId = Guid.NewGuid().ToString();
        string name = $"Key for {algorithm}";
        KeySerializationFormat format;
        KeyEncoding encoding;
        string key;

        switch (algorithm)
        {
            case SecurityAlgorithms.RsaSha256:
            case SecurityAlgorithms.RsaSha384:
            case SecurityAlgorithms.RsaSha512:
            case SecurityAlgorithms.RsaSsaPssSha256:
            case SecurityAlgorithms.RsaSsaPssSha384:
            case SecurityAlgorithms.RsaSsaPssSha512:

                var rsa = RSA.Create(rsaKeySizeInBits);
                var rsaParameters = rsa.ExportParameters(true);
                key = SerializeRsaToXml(rsaParameters);

                format = KeySerializationFormat.Xml;
                encoding = KeyEncoding.Plain;

                break;

            case SecurityAlgorithms.EcdsaSha256:

                key = ECKeyHelper.ExportECParametersToXml(ECDsa.Create(ECCurve.NamedCurves.nistP256), true);
                format = KeySerializationFormat.Xml;
                encoding = KeyEncoding.Plain;

                break;

            case SecurityAlgorithms.EcdsaSha384:

                key = ECKeyHelper.ExportECParametersToXml(ECDsa.Create(ECCurve.NamedCurves.nistP384), true);
                format = KeySerializationFormat.Xml;
                encoding = KeyEncoding.Plain;

                break;

            case SecurityAlgorithms.EcdsaSha512:

                key = ECKeyHelper.ExportECParametersToXml(ECDsa.Create(ECCurve.NamedCurves.nistP521), true);
                format = KeySerializationFormat.Xml;
                encoding = KeyEncoding.Plain;

                break;

            case SecurityAlgorithms.HmacSha256:
            case SecurityAlgorithms.HmacSha384:
            case SecurityAlgorithms.HmacSha512:

                byte[] bytes = new byte[32];
                RandomNumberGenerator.Fill(bytes);
                key = Convert.ToBase64String(bytes);

                format = KeySerializationFormat.None;
                encoding = KeyEncoding.Base64;

                break;

            default:
                throw new NotSupportedException($"The specified algorithm '{algorithm}' is not recognized or supported.");
        }

        var keyParameters = new KeyParameters(keyId, name, algorithm, format, encoding, key);

        if (lifetime.HasValue)
        {
            keyParameters.NotBefore = keyParameters.Created;
            keyParameters.Expires = keyParameters.Created.Add(lifetime.Value);
        }

        return keyParameters;
    }

    private static string SerializeRsaToXml(RSAParameters rsaParameters)
    {
        using var stringWriter = new StringWriter();
        var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters));
        xmlSerializer.Serialize(stringWriter, rsaParameters);
        return stringWriter.ToString();
    }
}
