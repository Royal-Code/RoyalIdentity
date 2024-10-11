using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace RoyalIdentity.Contexts;

public class AuthorizeContext : EndpointContextBase, IWithRedirectUri
{
    public AuthorizeContext(HttpContext httpContext, NameValueCollection raw, ContextItems? items = null) 
        : base(httpContext, raw, items)
    { }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject => HttpContext.User;

    public ClaimsIdentity? Identity => HttpContext.User?.Identity as ClaimsIdentity;

    /// <summary>
    /// Gets or sets the client model.
    /// </summary>
    public Client? Client { get; set; }

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    /// <remarks>
    /// The client secret is not received on the authorize endpoint.
    /// </remarks>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the redirect URI.
    /// </summary>
    /// <value>
    /// The redirect URI.
    /// </value>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the type of the response.
    /// </summary>
    /// <value>
    /// The type of the response.
    /// </value>
    public string? ResponseType { get; set; }

    /// <summary>
    /// Gets or sets the response mode.
    /// </summary>
    /// <value>
    /// The response mode.
    /// </value>
    public string? ResponseMode { get; set; }

    /// <summary>
    /// Gets or sets the grant type.
    /// </summary>
    /// <value>
    /// The grant type.
    /// </value>
    public string? GrantType { get; set; }

    /// <summary>
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public List<string> RequestedScopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>
    /// The state.
    /// </value>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the request was an OpenID Connect request.
    /// </summary>
    /// <value>
    /// <c>true</c> if the request was an OpenID Connect request; otherwise, <c>false</c>.
    /// </value>
    public bool IsOpenIdRequest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is API resource request.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is API resource request; otherwise, <c>false</c>.
    /// </value>
    public bool IsApiResourceRequest { get; set; }

    /// <summary>
    /// Gets or sets the nonce.
    /// </summary>
    /// <value>
    /// The nonce.
    /// </value>
    public string? Nonce { get; set; }

    /// <summary>
    /// Gets or sets the authentication context reference classes.
    /// </summary>
    /// <value>
    /// The authentication context reference classes.
    /// </value>
    public List<string> AuthenticationContextReferenceClasses { get; set; } = [];

    /// <summary>
    /// Gets or sets the display mode.
    /// </summary>
    /// <value>
    /// The display mode.
    /// </value>
    public string? DisplayMode { get; set; }

    /// <summary>
    /// Gets or sets the collection of prompt modes.
    /// </summary>
    /// <value>
    /// The collection of prompt modes.
    /// </value>
    public ICollection<string> PromptModes { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum age.
    /// </summary>
    /// <value>
    /// The maximum age.
    /// </value>
    public int? MaxAge { get; set; }

    /// <summary>
    /// Gets or sets the login hint.
    /// </summary>
    /// <value>
    /// The login hint.
    /// </value>
    public string? LoginHint { get; set; }

    /// <summary>
    /// Gets or sets the code challenge
    /// </summary>
    /// <value>
    /// The code challenge
    /// </value>
    public string? CodeChallenge { get; set; }

    /// <summary>
    /// Gets or sets the code challenge method
    /// </summary>
    /// <value>
    /// The code challenge method
    /// </value>
    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// Gets or sets the IdToken hint.
    /// </summary>
    public string? IdTokenHint { get; set; }

    /// <summary>
    /// Gets a value indicating whether an access token was requested.
    /// </summary>
    /// <value>
    /// <c>true</c> if an access token was requested; otherwise, <c>false</c>.
    /// </value>
    public bool AccessTokenRequested =>
        ResponseType == OidcConstants.ResponseTypes.IdTokenToken ||
        ResponseType == OidcConstants.ResponseTypes.Code ||
        ResponseType == OidcConstants.ResponseTypes.CodeIdToken ||
        ResponseType == OidcConstants.ResponseTypes.CodeToken ||
        ResponseType == OidcConstants.ResponseTypes.CodeIdTokenToken;

    /// <summary>
    /// The resources of the result.
    /// </summary>
    public Resources Resources { get; set; } = new();

    /// <summary>
    /// Gets the session id from the current user.
    /// </summary>
    public string? SessionId => Identity?.FindFirst(c => c.Type == JwtClaimTypes.SessionId)?.Value;

    public string? GetPrefixedAcrValue(string prefix)
    {
        var value = AuthenticationContextReferenceClasses.Find(x => x.StartsWith(prefix));

        if (value != null)
        {
            value = value.Substring(prefix.Length);
        }

        return value;
    }

    public void RemovePrefixedAcrValue(string prefix)
    {
        AuthenticationContextReferenceClasses.RemoveAll(acr => acr.StartsWith(prefix, StringComparison.Ordinal));
        var acr_values = AuthenticationContextReferenceClasses.ToSpaceSeparatedString();
        if (acr_values.IsPresent())
        {
            Raw[OidcConstants.AuthorizeRequest.AcrValues] = acr_values;
        }
        else
        {
            Raw.Remove(OidcConstants.AuthorizeRequest.AcrValues);
        }
    }

    public string? GetIdP()
    {
        return GetPrefixedAcrValue(Constants.KnownAcrValues.HomeRealm);
    }

    public void RemoveIdP()
    {
        RemovePrefixedAcrValue(Constants.KnownAcrValues.HomeRealm);
    }

#pragma warning disable CS8774

    [MemberNotNull(nameof(Client), nameof(ClientId))]
    public void AssertHasClient()
    {
        var has = Items.Get<Asserts>()?.HasClient ?? false;
        if (!has)
            throw new InvalidOperationException("Client was required, but is missing");
    }

    [MemberNotNull(nameof(RedirectUri), nameof(Client), nameof(ClientId))]
    public void AssertHasRedirectUri()
    {
        var has = Items.Get<Asserts>()?.HasRedirectUri ?? false;
        if (!has)
            throw new InvalidOperationException("RedirectUri was required, but is missing");
    }

    [MemberNotNull(nameof(GrantType), nameof(ResponseType))]
    public void AssertHasGrantType()
    {
        var has = Items.Get<Asserts>()?.HasGrantType ?? false;
        if (!has)
            throw new InvalidOperationException("GrantType was required, but is missing");
    }

    [MemberNotNull(nameof(ResponseType))]
    public void AssertHasResponseType()
    {
        var has = Items.Get<Asserts>()?.HasResponseType ?? false;
        if (!has)
            throw new InvalidOperationException("ResponseType was required, but is missing");
    }
}
