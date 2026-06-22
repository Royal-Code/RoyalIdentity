namespace RoyalIdentity.Security.Keys;

/// <summary>
/// How the key material is serialized inside the <see cref="KeyParameters.Key"/> string.
/// </summary>
public enum KeySerializationFormat
{
    /// <summary>JSON-serialized parameters (RSA/EC).</summary>
    Json = 0,

    /// <summary>XML-serialized parameters (RSA/EC).</summary>
    Xml = 1,

    /// <summary>No structured serialization; the value is the raw key bytes (symmetric/HMAC).</summary>
    None = 2,
}
