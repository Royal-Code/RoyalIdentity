using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts;

public class RevocationContext : EndpointContextBase, IWithClientCredentials
{
    public RevocationContext(HttpContext httpContext, NameValueCollection raw, ContextItems items)
        : base(httpContext, raw, items)
    {
        Token = Raw.Get(OidcConstants.RevocationRequest.Token);
        TokenTypeHint = Raw.Get(OidcConstants.RevocationRequest.TokenTypeHint);
    }

    public string? Token { get; }

    public string? TokenTypeHint { get; }

    public bool IsClientRequired => true;

    public Client? Client { get; private set; }

    public string? ClientId { get; private set; }

    public HashSet<Claim> ClientClaims { get; } = [];

    public EvaluatedCredential? ClientSecret { get; private set; }

    public string? Confirmation => ClientSecret?.Confirmation;

    public void SetClient(Client client)
    {
        Client = client;
        ClientId = client.Id;
        ClientClaims.AddRange(client.Claims.Select(c => new Claim(c.Type, c.Value, c.ValueType)));
    }

    public void SetClientAndSecret(Client client, EvaluatedCredential clientSecret)
    {
        SetClient(client);
        ClientSecret = clientSecret;
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