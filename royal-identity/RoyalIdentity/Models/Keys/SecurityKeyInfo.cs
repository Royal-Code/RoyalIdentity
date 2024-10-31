using Microsoft.IdentityModel.Tokens;

namespace RoyalIdentity.Models.Keys;

/// <summary>
/// Information about a security key
/// </summary>
public class SecurityKeyInfo
{
    /// <summary>
    /// The key
    /// </summary>
    public required SecurityKey Key { get; set; }

    /// <summary>
    /// The signing algorithm
    /// </summary>
    public required string SigningAlgorithm { get; set; }
}