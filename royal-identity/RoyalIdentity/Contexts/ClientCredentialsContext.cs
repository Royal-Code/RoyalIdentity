using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts;

public class ClientCredentialsContext : TokenEndpointContextBase, IWithResources
{
    private bool resourcesValidated;
    private ClaimsPrincipal? principal;

    public ClientCredentialsContext(
        HttpContext httpContext,
        NameValueCollection raw,
        ContextItems items) : base(httpContext, raw, GrantTypes.ClientCredentials, items)
    { }

    public Resources Resources { get; } = new();

    public override ClaimsPrincipal? GetSubject()
    {
        if (principal is not null)
            return principal;

        ClientParameters.AssertHasClientSecret();
        var client = ClientParameters.Client;

        var identity = new ClaimsIdentity();

        if (!client.AlwaysSendClientClaims || !client.Claims.Any(x => x.Type == JwtClaimTypes.Subject))
            identity.AddClaim(new Claim(JwtClaimTypes.Subject, client.Id));

        identity.AddClaim(new(JwtClaimTypes.AuthenticationTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
        identity.AddClaim(new(JwtClaimTypes.IdentityProvider, ServerConstants.LocalIdentityProvider));
        identity.AddClaim(new(JwtClaimTypes.AuthenticationMethod, ClientParameters.AuthenticationMethod));

        principal = new ClaimsPrincipal(identity);

        return principal;
    }

    public override void Load(ILogger logger) => LoadBase(logger);

    public void ResourcesValidated() => resourcesValidated = true;

    public void AssertResourcesValidated()
    {
        if (!resourcesValidated)
            throw new InvalidOperationException("Resources validated was required, but was not validated");
    }
}
