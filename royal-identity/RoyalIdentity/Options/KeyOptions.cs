using Microsoft.IdentityModel.Tokens;

namespace RoyalIdentity.Options;

/// <summary>
/// Options for key management.
/// </summary>
public class KeyOptions
{
    public KeyOptions()
    {
    }

    public KeyOptions(KeyOptions other)
    {
        MainSigningCredentialsAlgorithm = other.MainSigningCredentialsAlgorithm;
        DefaultSigningCredentialsLifetime = other.DefaultSigningCredentialsLifetime;
        RsaKeySizeInBits = other.RsaKeySizeInBits;

        SigningCredentialsAlgorithms.Clear();
        foreach (var algorithm in other.SigningCredentialsAlgorithms)
        {
            SigningCredentialsAlgorithms.Add(algorithm);
        }
    }

    /// <summary>
    /// The main signature credential algorithm
    /// </summary>
    public string MainSigningCredentialsAlgorithm { get; set; } = SecurityAlgorithms.EcdsaSha256;

    /// <summary>
    /// Default lifetime for new signature credentials.
    /// </summary>
    public TimeSpan? DefaultSigningCredentialsLifetime { get; set; } = TimeSpan.FromDays(356);

    /// <summary>
    /// The size, in bits, used to create a new RSA signing credential.
    /// </summary>
    public int RsaKeySizeInBits { get; set; } = 2048;

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
