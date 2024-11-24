using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contexts;

public abstract class TokenEndpointContextBase : EndpointContextBase, ITokenEndpointContextBase
{
    protected TokenEndpointContextBase(
        HttpContext httpContext, 
        NameValueCollection raw,
        string grantType,
        ContextItems? items = null) : base(httpContext, raw, items)
    {
        GrantType = grantType;
    }

    public string GrantType { get; }

    public EvaluatedCredential? ClientSecret { get; private set; }

    public string? Confirmation => ClientSecret?.Confirmation;

    public bool IsClientRequired => true;

    public Client? Client { get; private set; }

    public string? ClientId { get; protected set; }

    public HashSet<Claim> ClientClaims { get; } = [];

    public abstract void Load(ILogger logger);

    public abstract ClaimsPrincipal? GetSubject();

    public void SetClient(Client client)
    {
        Client = client;
        ClientClaims.AddRange(client.Claims.Select(c => new Claim(c.Type, c.Value, c.ValueType)));
    }

    public void SetClientAndSecret(Client client, EvaluatedCredential clientSecret)
    {
        SetClient(client);
        ClientSecret = clientSecret;
        ClientId = client.Id;
    }

#pragma warning disable CS8774

    private bool hasClient;

    [MemberNotNull(nameof(Client), nameof(ClientId), nameof(ClientSecret))]
    public void AssertHasClient()
    {
        if (hasClient)
            return;

        hasClient = Items.Get<Asserts>()?.HasClient ?? false;
        if (!hasClient)
            throw new InvalidOperationException("Client was required, but is missing");
    }
}
