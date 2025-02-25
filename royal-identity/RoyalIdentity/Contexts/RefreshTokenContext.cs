using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Contexts.Parameters;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Endpoints.Abstractions;
using System.Collections.Specialized;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts;

public class RefreshTokenContext : TokenEndpointContextBase, IWithRefreshToken
{
    private ClaimsPrincipal? subject;

    public RefreshTokenContext(
        HttpContext httpContext,
        NameValueCollection raw,
        ContextItems? items = null) : base(httpContext, raw, OpenIdConnectGrantTypes.RefreshToken, items)
    { }

    public string? Token { get; private set; }

    public RefreshParameters RefreshParameters { get; } = new();

    public override ClaimsPrincipal? GetSubject()
    {
        if (subject is null && RefreshParameters.RefreshToken is not null)
        {
            subject = RefreshParameters.RefreshToken.CreatePrincipal();
        }

        return subject;
    }

    public override void Load(ILogger logger)
    {
        ClientId = Raw.Get(TokenRequest.ClientId);
        Scope = Raw.Get(TokenRequest.Scope);
        Token = Raw.Get(TokenRequest.RefreshToken);
    }
}
