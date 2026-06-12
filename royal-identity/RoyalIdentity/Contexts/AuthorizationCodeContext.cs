using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Pipelines.Abstractions;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using RoyalIdentity.Contexts.Parameters;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Extensions;

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

    public HashSet<string> RequestedResourceUris { get; } = [];

    public override ClaimsPrincipal? GetSubject() => CodeParameters.AuthorizationCode?.Subject;

    public override void Load(ILogger logger)
    {
        LoadBase(logger);
        RedirectUri = Raw.Get(Oidc.Token.Request.RedirectUri);
        Code = Raw.Get(Oidc.Token.Request.Code);
        CodeVerifier = Raw.Get(Oidc.Token.Request.CodeVerifier);
        RequestedResourceUris.Clear();
        RequestedResourceUris.AddRange(Raw.GetValues(Oidc.Token.Request.Resource) ?? []);
    }

    public void RedirectUriValidated() => redirectUriValidated = true;

    [MemberNotNull(nameof(RedirectUri))]
    public void AssertHasRedirectUri()
    {
        if (!redirectUriValidated || RedirectUri is null)
            throw new InvalidOperationException("Redirect Uri was required, but is missing or not validated");
    }
}
