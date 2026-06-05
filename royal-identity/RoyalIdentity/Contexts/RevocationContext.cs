using System.Collections.Specialized;
using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts.Parameters;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts;

public class RevocationContext : EndpointContextBase, IWithClient
{
    public RevocationContext(HttpContext httpContext, NameValueCollection raw, ContextItems items)
        : base(httpContext, raw, items)
    {
        Token = Raw.Get(Oidc.Revocation.Request.Token);
        TokenTypeHint = Raw.Get(Oidc.Revocation.Request.TokenTypeHint);
    }

    public string? Token { get; }

    public string? TokenTypeHint { get; }

    public bool IsClientRequired => true;

    public string? ClientId => ClientParameters.Client?.Id;

    public ClientParameters ClientParameters { get; } = new();

}