using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Endpoints.Abstractions;
using System.Collections.Specialized;
using System.Security.Claims;
using RoyalIdentity.Contexts.Parameters;
using static RoyalIdentity.Options.OidcConstants;

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

    public string? Scope { get; protected set; }

    public bool IsClientRequired => true;

    public string? ClientId { get; protected set; }

    public ClientParameters ClientParameters { get; } = new();

    public abstract void Load(ILogger logger);

    public abstract ClaimsPrincipal? GetSubject();

    protected void LoadBase(ILogger logger)
    {
        ClientId = Raw.Get(TokenRequest.ClientId);
        Scope = Raw.Get(TokenRequest.Scope);
    }
}
