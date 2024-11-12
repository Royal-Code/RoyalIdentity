using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Represents a secret extracted from the HttpContext
/// </summary>
[Redesign("Trocar nome (?SecretChecked?), não necessidade de carregar dados dos segredos, mas sim retornar alguns dados identificando o tipo e resultado da validação")]
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
