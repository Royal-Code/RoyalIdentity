using Microsoft.IdentityModel.Tokens;

namespace RoyalIdentity.Models.Keys;

public class KeyParameters
{
    public KeyParameters(string keyId,
        string name,
        string securityAlgorithm,
        KeySerializationFormat format,
        KeyEncoding encoding,
        string key)
    {
        KeyId = keyId;
        Name = name;
        SecurityAlgorithm = securityAlgorithm;
        Format = format;
        Encoding = encoding;
        Key = key;
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

    public DateTime? NotBefore { get; set; }

    public DateTime? Expires { get; set; }
}