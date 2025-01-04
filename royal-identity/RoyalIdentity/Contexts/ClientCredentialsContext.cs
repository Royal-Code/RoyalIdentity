using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models;
using System.Collections.Specialized;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts;

public class ClientCredentialsContext : TokenEndpointContextBase
{
    public ClientCredentialsContext(
        HttpContext httpContext,
        NameValueCollection raw,
        ContextItems items) : base(httpContext, raw, GrantTypes.ClientCredentials, items)
    {

    }

    public Resources Resources { get; }

    public override ClaimsPrincipal? GetSubject() => null;

    public override void Load(ILogger logger)
    {
        throw new NotImplementedException();
    }
}
