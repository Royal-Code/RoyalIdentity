namespace RoyalIdentity.Options;

public class KeyOptions
{
    /// <summary>
    /// The main signature credential algorithm
    /// </summary>
    public string MainSigningCredentialsAlgorithm { get; set; } = OidcConstants.Algorithms.Asymmetric.RS512;

    /// <summary>
    /// The signing credentials algorithms allowed.
    /// </summary>
    public IEnumerable<string> SigningCredentialsAlgorithms { get; set; } = 
    [
        OidcConstants.Algorithms.Asymmetric.RS256,
        OidcConstants.Algorithms.Asymmetric.RS384,
        OidcConstants.Algorithms.Asymmetric.RS512,
        OidcConstants.Algorithms.Asymmetric.ES256,
        OidcConstants.Algorithms.Asymmetric.ES384,
        OidcConstants.Algorithms.Asymmetric.ES512,
        OidcConstants.Algorithms.Symmetric.HS256
    ];
}
