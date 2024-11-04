using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Serialization;

namespace RoyalIdentity.Models.Keys;

public class KeyParameters
{
    public static KeyParameters Create(string alg, int keySize = 0)
    {
        // Validate if the algorithm is supported
        if (!ServerConstants.SupportedSigningAlgorithms.Contains(alg))
        {
            throw new NotSupportedException($"The specified algorithm '{alg}' is not supported.");
        }

        // Generates key parameters and additional information based on the algorithm
        string keyId = Guid.NewGuid().ToString();
        string name = $"Key for {alg}";
        KeySerializationFormat format = KeySerializationFormat.Json; // ou o que for apropriado
        KeyEncoding encoding = KeyEncoding.Base64; // ou o que for apropriado
        string key;

        // Generates a key based on the algorithm
        switch (alg)
        {
            case SecurityAlgorithms.RsaSha256:
            case SecurityAlgorithms.RsaSha384:
            case SecurityAlgorithms.RsaSha512:
                key = GenerateRsaKey(keySize);
                break;

            case SecurityAlgorithms.RsaSsaPssSha256:
            case SecurityAlgorithms.RsaSsaPssSha384:
            case SecurityAlgorithms.RsaSsaPssSha512:
                key = GenerateRsaPssKey(keySize);
                break;

            case SecurityAlgorithms.EcdsaSha256:
            case SecurityAlgorithms.EcdsaSha384:
            case SecurityAlgorithms.EcdsaSha512:
                key = GenerateEcdsaKey();
                break;

            case SecurityAlgorithms.HmacSha256:
            case SecurityAlgorithms.HmacSha384:
            case SecurityAlgorithms.HmacSha512:
                key = GenerateHmacKey();
                break;

            default:
                throw new NotSupportedException($"The specified algorithm '{alg}' is not recognized.");
        }

        // Cria e retorna a nova instância de KeyParameters
        return new KeyParameters(keyId, name, alg, format, encoding, key);
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
}