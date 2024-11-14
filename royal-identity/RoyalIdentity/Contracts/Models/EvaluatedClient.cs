using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Models;

/// <summary>
/// Represents a secret extracted from the HttpContext
/// </summary>
public class EvaluatedClient
{
    public EvaluatedClient(Client client, EvaluatedCredential credential)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    /// <summary>
    /// Gets or sets the identifier associated with this secret
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    public Client Client { get; }

    /// <summary>
    /// Gets or sets the credential to verify the secret
    /// </summary>
    /// <value>
    /// The credential.
    /// </value>
    public EvaluatedCredential Credential { get; }

    ///// <summary>
    ///// Gets or sets additional properties.
    ///// </summary>
    ///// <value>
    ///// The properties.
    ///// </value>
    //public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}
