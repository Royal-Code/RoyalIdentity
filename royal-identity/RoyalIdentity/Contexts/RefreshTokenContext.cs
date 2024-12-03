using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models.Tokens;
using System.Collections.Specialized;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts;

public class RefreshTokenContext : TokenEndpointContextBase
{
    public RefreshTokenContext(
        HttpContext httpContext,
        NameValueCollection raw,
        string grantType,
        ContextItems? items = null) : base(httpContext, raw, grantType, items)
    {

    }

    public string? Token { get; private set; }

    public RefreshToken? RefreshToken { get; set; }

    public override ClaimsPrincipal? GetSubject()
    {
        throw new NotImplementedException();
    }

    public override void Load(ILogger logger)
    {
        ClientId = Raw.Get(TokenRequest.ClientId);
        Scope = Raw.Get(TokenRequest.Scope);
        Token = Raw.Get(TokenRequest.RefreshToken);

        throw new NotImplementedException();
    }
}
