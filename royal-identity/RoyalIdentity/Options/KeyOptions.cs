namespace RoyalIdentity.Options;

public class KeyOptions
{
    /// <summary>
    /// The main signature credential algorithm
    /// </summary>
    public string MainSigningCredentialsAlgorithm { get; set; } = OidcConstants.Algorithms.Asymmetric.RS256;

    /// <summary>
    /// Default lifetime for new signature credentials.
    /// </summary>
    public TimeSpan DefaultSigningCredentialsLifetime { get; set; } = TimeSpan.FromDays(356);

    /// <summary>
    /// The size to create a new RSA signature credential.
    /// </summary>
    public int RsaKeySizeInBytes { get; set; } = 2048;

    /// <summary>
    /// The signing credentials algorithms allowed.
    /// </summary>
    public HashSet<string> SigningCredentialsAlgorithms { get; } = 
    [
        OidcConstants.Algorithms.Asymmetric.RS256,
        OidcConstants.Algorithms.Asymmetric.RS384,
        OidcConstants.Algorithms.Asymmetric.RS512,
        OidcConstants.Algorithms.Asymmetric.PS256,
        OidcConstants.Algorithms.Asymmetric.PS384,
        OidcConstants.Algorithms.Asymmetric.PS512,
        OidcConstants.Algorithms.Asymmetric.ES256,
        OidcConstants.Algorithms.Asymmetric.ES384,
        OidcConstants.Algorithms.Asymmetric.ES512,
        OidcConstants.Algorithms.Symmetric.HS256,
        OidcConstants.Algorithms.Symmetric.HS384,
        OidcConstants.Algorithms.Symmetric.HS512,
    ];
}
