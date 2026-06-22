using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Serialization;

namespace RoyalIdentity.Security.Keys;

/// <summary>
/// Reusable, serializable description of a signing/validation key: its identifier, algorithm,
/// serialization format/encoding and the serialized key material. It can materialize the matching
/// <see cref="SecurityKey"/>/<see cref="SigningCredentials"/> on demand and project the public
/// <see cref="JsonWebKey"/> for validation, without leaking private material.
/// </summary>
/// <remarks>
/// This type lives in the leaf security library and intentionally knows nothing about the IdP
/// (no realm, client, or <c>KeyOptions</c>). Generating fresh key material from an algorithm lives in
/// <see cref="KeyMaterialFactory"/>; the IdP keeps its own <c>KeyOptions</c>-aware factory in the core.
/// </remarks>
public class KeyParameters
{
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

    public (SecurityKey Key, JsonWebKey? JsonWebKey) GetValidationKey()
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
            jwk.Alg = SecurityAlgorithm;
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
            jwk.Alg = SecurityAlgorithm;
        }
        else if (SecurityAlgorithm.StartsWith("HS"))
        {
            key = CreateSymmetricSecurityKey();

            // No JWK is returned for symmetric validation keys, as they contain private material only.
            jwk = null;
        }
        else
        {
            throw new NotSupportedException("SecurityAlgorithm not supported");
        }

        return (key, jwk);
    }
}
