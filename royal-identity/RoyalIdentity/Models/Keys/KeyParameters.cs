using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Serialization;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Models.Keys;

public class KeyParameters
{
    /// <summary>
    /// <para>
    ///     Creates a new <see cref="KeyParameters"/> for the given algorithm.
    /// </para>
    /// </summary>
    /// <param name="algorithm">Secutiry algorithm, see <see cref="SecurityAlgorithms"/>.</param>
    /// <param name="lifetime">Key lifetime.</param>
    /// <param name="keySize">For RSA keys, the key size must be entered. If not entered, 2048 will be used.</param>
    /// <returns>
    ///     A new instance of <see cref=�KeyParameters�/>.
    /// </returns>
    /// <exception cref="NotSupportedException">
    ///     Occurs when the algorithm is not supported.
    /// </exception>
    public static KeyParameters Create(string algorithm, TimeSpan? lifetime = null, int keySize = 0)
    {
        // Validate if the algorithm is supported
        if (!ServerConstants.SupportedSigningAlgorithms.Contains(algorithm))
        {
            throw new NotSupportedException($"The specified algorithm '{algorithm}' is not supported.");
        }

        // Generates key parameters and additional information based on the algorithm
        string keyId = Guid.NewGuid().ToString();
        string name = $"Key for {algorithm}";
        KeySerializationFormat format;
        KeyEncoding encoding;
        string key;

        // Generates a key based on the algorithm
        switch (algorithm)
        {
            case SecurityAlgorithms.RsaSha256:
            case SecurityAlgorithms.RsaSha384:
            case SecurityAlgorithms.RsaSha512:
            case SecurityAlgorithms.RsaSsaPssSha256:
            case SecurityAlgorithms.RsaSsaPssSha384:
            case SecurityAlgorithms.RsaSsaPssSha512:

                var rsa = RSA.Create(keySize is 0 ? 2048 : keySize);
                var rsaParameters = rsa.ExportParameters(true);
                key = SerializeToXml(rsaParameters);

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
                encoding = KeyEncoding.Plain;

                break;

            default:
                throw new NotSupportedException($"The specified algorithm '{algorithm}' is not recognized or supported.");
        }



        // Cria e retorna a nova instância de KeyParameters
        var keyParameters = new KeyParameters(keyId, name, algorithm, format, encoding, key);

        if (lifetime.HasValue)
        {
            keyParameters.NotBefore = keyParameters.Created;
            keyParameters.Expires = keyParameters.Created.Add(lifetime.Value);
        }

        return keyParameters;
    }

    public KeyParameters(string keyId,
        string name,
        string securityAlgorithm,
        KeySerializationFormat format,
        KeyEncoding encoding,
        string key,
        DateTime? created = null)
    {
        KeyId = keyId;
        Name = name;
        SecurityAlgorithm = securityAlgorithm;
        Format = format;
        Encoding = encoding;
        Key = key;
        Created = created ?? DateTime.UtcNow;
    }

    public string KeyId { get; }

    public string Name { get; }

    /// <summary>
    /// Defines the algorithm for the key (<see cref="SecurityAlgorithms"/>).
    /// </summary>
    public string SecurityAlgorithm { get; }

    public KeySerializationFormat Format { get; }

    public KeyEncoding Encoding { get; }

    public string Key { get; }

    public DateTime Created { get; set; }

    public DateTime? NotBefore { get; set; }

    public DateTime? Expires { get; set; }

    public virtual SigningCredentials CreateSigningCredentials()
    {
        return new SigningCredentials(GetSecurityKey(), SecurityAlgorithm);
    }

    public virtual SecurityKey GetSecurityKey()
    {
        if (SecurityAlgorithm.StartsWith("RS"))
        {
            return CreateRsaSecurityKey();
        }
        else if (SecurityAlgorithm.StartsWith("PS"))
        {
            return CreateRsaSecurityKey();
        }
        else if (SecurityAlgorithm.StartsWith("ES"))
        {
            return CreateECDsaSecurityKey();
        }
        else if (SecurityAlgorithm.StartsWith("HS"))
        {
            return CreateSymmetricSecurityKey();
        }

        throw new NotSupportedException("SecurityAlgorithm not supported");
    }

    public virtual RsaSecurityKey CreateRsaSecurityKey()
    {
        if (!SecurityAlgorithm.StartsWith("RS") && !SecurityAlgorithm.StartsWith("PS"))
        {
            throw new InvalidOperationException("The secret type must be RSA.");
        }

        RSAParameters rsaParameters;
        if (Format == KeySerializationFormat.Xml)
        {
            using StringReader textReader = new(DecodeKey());
            var xml = new XmlSerializer(typeof(RSAParameters));
            rsaParameters = (RSAParameters)xml.Deserialize(textReader)!;
        }
        else
        {
            rsaParameters = JsonSerializer.Deserialize<RSAParameters>(DecodeKey(), new JsonSerializerOptions()
            {
                IncludeFields = true
            });
        }

        return new RsaSecurityKey(RSA.Create(rsaParameters))
        {
            KeyId = KeyId
        };
    }

    public virtual ECDsaSecurityKey CreateECDsaSecurityKey()
    {
        if (!SecurityAlgorithm.StartsWith("ES"))
        {
            throw new InvalidOperationException("The secret type must be ECDsa.");
        }

        ECParameters ecParameters;
        if (Format == KeySerializationFormat.Xml)
        {
            ecParameters = ECKeyHelper.ImportECParametersFromXml(DecodeKey());
        }
        else
        {
            ecParameters = ECKeyHelper.DeserializeECParameters(DecodeKey());
        }

        return new ECDsaSecurityKey(ECDsa.Create(ecParameters))
        {
            KeyId = KeyId
        };
    }

    public virtual SymmetricSecurityKey CreateSymmetricSecurityKey()
    {
        if (!SecurityAlgorithm.StartsWith("HS"))
        {
            throw new InvalidOperationException("The secret type must be Symmetric.");
        }

        return new SymmetricSecurityKey(GetKeyBytes())
        {
            KeyId = KeyId,
        };
    }

    private string DecodeKey()
    {
        if (Encoding == KeyEncoding.Plain)
            return Key;

        byte[] bytes = Encoding == KeyEncoding.Base64
            ? Convert.FromBase64String(Key)
            : Convert.FromHexString(Key);

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private byte[] GetKeyBytes()
    {
        if (Encoding == KeyEncoding.Plain)
            throw new InvalidOperationException("You can't get the bytes because there's no encoding (plain)");

        return Encoding == KeyEncoding.Base64
            ? Convert.FromBase64String(Key)
            : Convert.FromHexString(Key);
    }

    private static string SerializeToXml(RSAParameters rsaParameters)
    {
        using var stringWriter = new StringWriter();
        var xmlSerializer = new XmlSerializer(typeof(RSAParameters));
        xmlSerializer.Serialize(stringWriter, rsaParameters);
        return stringWriter.ToString();
    }

    public (SecurityKey, JsonWebKey?) GetValidationKey()
    {
        SecurityKey key;
        JsonWebKey? jwk;

        if (SecurityAlgorithm.StartsWith("RS") || SecurityAlgorithm.StartsWith("PS"))
        {
            var rsaKey = CreateRsaSecurityKey();
            key = rsaKey;

            // if rsaKey has a private key, we need to convert it to a public key
            if (rsaKey.PrivateKeyStatus == PrivateKeyStatus.Exists)
            {
                rsaKey = rsaKey.WithoutPrivateKey();
            }

            // generate the JWK from the RSA key
            jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(rsaKey);
            jwk.KeyId = KeyId;
        }
        else if (SecurityAlgorithm.StartsWith("ES"))
        {
            var ecKey = CreateECDsaSecurityKey();
            key = ecKey;

            // if ecKey has a private key, we need to convert it to a public key
            if (ecKey.PrivateKeyStatus == PrivateKeyStatus.Exists)
            {
                ecKey = ecKey.WithoutPrivateKey();
            }

            // generate the JWK from the EC key
            jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(ecKey);
            jwk.KeyId = KeyId;
        }
        else if (SecurityAlgorithm.StartsWith("HS"))
        {
            key = CreateSymmetricSecurityKey();

            // Não será retornado a JWK para chaves simétricas de validação, pois há apenas conteúdo privado.
            jwk = null;
        }
        else
        {
            throw new NotSupportedException("SecurityAlgorithm not supported");
        }

        return (key, jwk);
    }
}