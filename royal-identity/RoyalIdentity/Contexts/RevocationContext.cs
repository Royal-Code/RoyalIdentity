using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts.Parameters;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Endpoints.Abstractions;
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

    public string? ClientId { get; private set; }

    public ClientParameters ClientParameters { get; } = new();

}