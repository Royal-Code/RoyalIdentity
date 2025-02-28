using Microsoft.IdentityModel.Tokens;

namespace RoyalIdentity.Options;

/// <summary>
/// Options for key management.
/// </summary>
public class KeyOptions
{
    /// <summary>
    /// The main signature credential algorithm
    /// </summary>
    public string MainSigningCredentialsAlgorithm { get; set; } = SecurityAlgorithms.EcdsaSha256;

    /// <summary>
    /// Default lifetime for new signature credentials.
    /// </summary>
    public TimeSpan? DefaultSigningCredentialsLifetime { get; set; } = TimeSpan.FromDays(356);

    /// <summary>
    /// The size to create a new RSA signature credential.
    /// </summary>
    public int RsaKeySizeInBytes { get; set; } = 2048;

    /// <summary>
    /// The signing credentials algorithms allowed.
    /// </summary>
    public HashSet<string> SigningCredentialsAlgorithms { get; } = 
    [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512,

        SecurityAlgorithms.HmacSha256,
        SecurityAlgorithms.HmacSha384,
        SecurityAlgorithms.HmacSha512,
    ];
}
