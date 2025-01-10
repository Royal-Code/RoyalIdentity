using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace RoyalIdentity.Contexts.Parameters;

public class ClientParameters
{
    public Client? Client { get; private set; }

    public string? ClientId => Client?.Id;

    public EvaluatedCredential? ClientSecret { get; private set; }

    /// <summary>
    /// Gets or sets the value of the confirmation method (will become the cnf claim). Must be a JSON object.
    /// </summary>
    /// <value>
    /// The confirmation.
    /// </value>
    public string? Confirmation => ClientSecret?.Confirmation;

    /// <summary>
    /// Gets or sets the client claims for the current request.
    /// This value is initally read from the client configuration but can be modified in the request pipeline
    /// </summary>
    [Redesign("Use only in ClientCredentialsContext --  remove")]
    public HashSet<Claim> ClientClaims { get; } = [];


    [MemberNotNull(nameof(Client), nameof(ClientId))]
    public void AssertHasClient()
    {
        if (Client is null || ClientId is null)
            throw new InvalidOperationException("Client was required, but is missing");
    }

    [MemberNotNull(nameof(ClientSecret), nameof(Client), nameof(ClientId))]
    public void AssertHasClientSecret()
    {
        if (ClientSecret is null || Client is null || ClientId is null)
            throw new InvalidOperationException("ClientSecret was required, but is missing");
    }

    public void SetClient(Client client)
    {
        Client = client;
        ClientClaims.AddRange(client.Claims.Select(c => new Claim(c.Type, c.Value, c.ValueType)));
    }

    public void SetClientAndSecret(Client client, EvaluatedCredential clientSecret)
    {
        SetClient(client);
        ClientSecret = clientSecret;
    }
}
