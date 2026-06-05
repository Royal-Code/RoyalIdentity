using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using System.Security.Claims;
using RoyalIdentity.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Models.Resources;

namespace RoyalIdentity.Contexts;

public class ClientCredentialsContext : TokenEndpointContextBase, IWithResources
{
    private bool resourcesValidated;
    private ClaimsPrincipal? principal;

    public ClientCredentialsContext(
        HttpContext httpContext,
        NameValueCollection raw,
        ContextItems items) : base(httpContext, raw, OpenIdConnectGrantTypes.ClientCredentials, items)
    { }

    public RequestedScopes Scopes { get; } = new();

    public override ClaimsPrincipal? GetSubject()
    {
        if (principal is not null)
            return principal;

        ClientParameters.AssertHasClientSecret();
        var client = ClientParameters.Client;

        var identity = new ClaimsIdentity();

        if (!client.AlwaysSendClientClaims || client.Claims.All(x => x.Type != JwtRegisteredClaimNames.Sub))
            identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, client.Id));

        identity.AddClaim(new(JwtRegisteredClaimNames.AuthTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
        identity.AddClaim(new(Jwt.ClaimTypes.IdentityProvider, Server.LocalIdentityProvider));
        identity.AddClaim(new(JwtRegisteredClaimNames.Amr, ClientParameters.AuthenticationMethod));

        principal = new ClaimsPrincipal(identity);

        return principal;
    }

    public override void Load(ILogger logger)
    {
        LoadBase(logger);
        Scopes.Scopes.AddRange(Scope.FromSpaceSeparatedString());
    }

    public void ResourcesValidated() => resourcesValidated = true;

    public void AssertResourcesValidated()
    {
        if (!resourcesValidated)
            throw new InvalidOperationException("Resources validated was required, but was not validated");
    }
}
