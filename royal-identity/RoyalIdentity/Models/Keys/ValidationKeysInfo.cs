using Microsoft.IdentityModel.Tokens;

namespace RoyalIdentity.Models.Keys;

/// <summary>
/// Information about validations security keys.
/// </summary>
public class ValidationKeysInfo
{
    /// <summary>
    /// All validations keys.
    /// </summary>
    public required IReadOnlyList<SecurityKey> Keys { get; set; }

    /// <summary>
    /// All validations keys as <see cref="JsonWebKey"/>.
    /// </summary>
    public required IReadOnlyList<JsonWebKey> Jwks { get; set; }
}