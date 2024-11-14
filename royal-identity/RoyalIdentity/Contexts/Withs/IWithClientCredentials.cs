using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithClientCredentials : IWithClient
{
    public EvaluatedCredential? ClientSecret { get; }

    /// <summary>
    /// Gets or sets the value of the confirmation method (will become the cnf claim). Must be a JSON object.
    /// </summary>
    /// <value>
    /// The confirmation.
    /// </value>
    public string? Confirmation { get; }

    public void SetClientAndSecret(Client client, EvaluatedCredential clientSecret);
}