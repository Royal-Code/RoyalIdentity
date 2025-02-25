using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Endpoints.Abstractions;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using static RoyalIdentity.Options.OidcConstants;
using RoyalIdentity.Contexts.Parameters;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace RoyalIdentity.Contexts;

public class AuthorizationCodeContext : TokenEndpointContextBase, IWithAuthorizationCode, IWithRedirectUri
{
    private bool redirectUriValidated;

    public AuthorizationCodeContext(
        HttpContext httpContext,
        NameValueCollection raw,
        ContextItems? items = null) : base(httpContext, raw, OpenIdConnectGrantTypes.AuthorizationCode, items)
    { }

    public string? RedirectUri { get; private set; }

    public string? Code { get; private set; }

    public string? CodeVerifier { get; private set; }

    public CodeParameters CodeParameters { get; } = new();

    public override ClaimsPrincipal? GetSubject() => CodeParameters.AuthorizationCode?.Subject;

    public override void Load(ILogger logger)
    {
        LoadBase(logger);
        RedirectUri = Raw.Get(TokenRequest.RedirectUri);
        Code = Raw.Get(TokenRequest.Code);
        CodeVerifier = Raw.Get(TokenRequest.CodeVerifier);
    }

    public void RedirectUriValidated() => redirectUriValidated = true;

    [MemberNotNull(nameof(RedirectUri))]
    public void AssertHasRedirectUri()
    {
        if (!redirectUriValidated || RedirectUri is null)
            throw new InvalidOperationException("Redirect Uri was required, but is missing or not validated");
    }
}
