using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Models;

/// <summary>
/// Represents a secret extracted from the HttpContext
/// </summary>
public class EvaluatedClient
{
    public EvaluatedClient(Client client, EvaluatedCredential credential, string authenticationMethod)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Credential = credential ?? throw new ArgumentNullException(nameof(credential));
        AuthenticationMethod = authenticationMethod ?? throw new ArgumentNullException(nameof(authenticationMethod));
    }

    /// <summary>
    /// Gets the identifier associated with this secret
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    public Client Client { get; }

    /// <summary>
    /// Gets the credential to verify the secret
    /// </summary>
    /// <value>
    /// The credential.
    /// </value>
    public EvaluatedCredential Credential { get; }

    /// <summary>
    /// Gets the authentication method.
    /// </summary>
    /// <value>
    /// The authentication method.
    /// </value>
    public string AuthenticationMethod { get; }
}
