using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models.Tokens;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using RoyalIdentity.Models;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts;

public class AuthorizationCodeContext : TokenEndpointContextBase, IWithRedirectUri
{
    public AuthorizationCodeContext(
        HttpContext httpContext,
        NameValueCollection raw,
        string grantType,
        ContextItems? items = null) : base(httpContext, raw, grantType, items)
    {
    }

    public string? RedirectUri { get; private set; }

    public string? Code { get; private set; }

    public string? CodeVerifier { get; private set; }

    public AuthorizationCode? AuthorizationCode { get; set; }

    public Resources? Resources { get; set; }

    public override ClaimsPrincipal? GetSubject()
    {
        return AuthorizationCode?.Subject;
    }

    public override void Load(ILogger logger)
    {
        ClientId = Raw.Get(TokenRequest.ClientId);
        Scope = Raw.Get(TokenRequest.Scope);
        RedirectUri = Raw.Get(TokenRequest.RedirectUri);
        Code = Raw.Get(TokenRequest.Code);
        CodeVerifier = Raw.Get(TokenRequest.CodeVerifier);
    }

#pragma warning disable CS8774

    private bool hasCode;
    private bool hasRedirectUri;

    [MemberNotNull(nameof(Code), nameof(AuthorizationCode), nameof(Resources))]
    public void AssertHasCode()
    {
        if (hasCode)
            return;

        hasCode = Items.Get<Asserts>()?.HasCode ?? false;
        if (!hasCode)
            throw new InvalidOperationException("Code was required, but is missing");
    }

    [MemberNotNull(nameof(RedirectUri))]
    public void AssertHasRedirectUri()
    {
        if (hasRedirectUri)
            return;

        hasRedirectUri = Items.Get<Asserts>()?.HasRedirectUri ?? false;
        if (!hasRedirectUri)
            throw new InvalidOperationException("Redirect Uri was required, but is missing");
    }
}
