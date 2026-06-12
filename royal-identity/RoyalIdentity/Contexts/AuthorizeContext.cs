using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Parameters;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace RoyalIdentity.Contexts;

public class AuthorizeContext : EndpointContextBase, IAuthorizationContextBase, IWithCodeChallenge
{
    private bool redirectUriValidated;
    private bool resourcesValidated;

    public AuthorizeContext(
        HttpContext httpContext,
        NameValueCollection raw,
        ClaimsPrincipal? subject = null,
        ContextItems? items = null) : base(httpContext, raw, items)
    {
        Subject = subject ?? httpContext.User;
    }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; }

    /// <summary>
    /// Gets the subject identity.
    /// </summary>
    public ClaimsIdentity? Identity => Subject?.Identity as ClaimsIdentity;

    /// <summary>
    /// Client is always required for authorize endpoint.
    /// </summary>
    public bool IsClientRequired => true;

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string? ClientId { get; private set; }

    /// <summary>
    /// Client parameters.
    /// </summary>
    public ClientParameters ClientParameters { get; } = new();

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
    public HashSet<string> ResponseTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the response mode.
    /// </summary>
    /// <value>
    /// The response mode.
    /// </value>
    public string? ResponseMode { get; set; }

    /// <summary>
    /// Gets or sets the nonce.
    /// </summary>
    /// <value>
    /// The nonce.
    /// </value>
    public string? Nonce { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>
    /// The state.
    /// </value>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the state hash.
    /// </summary>
    /// <value>
    /// The hash of the <see cref="State"/>.
    /// </value>
    public string? StateHash { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the resource owner denied consent on the consent screen.
    /// When <c>true</c>, the pipeline emits an <c>access_denied</c> error back to the client.
    /// </summary>
    public bool UserDeniedConsent { get; set; }

    /// <summary>
    /// Gets or sets the authentication context reference classes.
    /// </summary>
    /// <value>
    /// The authentication context reference classes.
    /// </value>
    public HashSet<string> AcrValues { get; } = [];

    /// <summary>
    /// Gets or sets the display mode.
    /// </summary>
    /// <value>
    /// The display mode.
    /// </value>
    public string? DisplayMode { get; set; }

    /// <summary>
    /// Gets or sets the UI locales.
    /// </summary>
    /// <value>
    /// The UI locales.
    /// </value>
    public string? UiLocales { get; set; }

    /// <summary>
    /// Gets or sets the collection of prompt modes.
    /// </summary>
    /// <value>
    /// The collection of prompt modes.
    /// </value>
    public HashSet<string> PromptModes { get; set; } = [];

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
    /// Gets ir sets the <c>scope</c>.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// The requested scopes.
    /// </summary>
    public RequestedResources Scopes { get; } = new();

    /// <summary>
    /// Gets the session id from the current user.
    /// </summary>
    public string? SessionId => Identity?.FindFirst(c => c.Type == JwtRegisteredClaimNames.Sid)?.Value;

    /// <summary>
    /// <para>
    ///     Use <see cref="EndpointContextBase.Raw"/> to call <see cref="Load(NameValueCollection, ILogger)"/>.
    /// </para>
    /// </summary>
    /// <param name="logger">The logger</param>
    public void Load(ILogger logger) => Load(Raw, logger);

    /// <summary>
    /// <para>
    ///     Load the parameters from the <paramref name="raw"/> and sets the <see cref="AuthorizeContext"/> 
    ///     properties.
    /// </para>
    /// </summary>
    /// <param name="raw">The parameters.</param>
    /// <param name="logger">The logger.</param>
    public virtual void Load(NameValueCollection raw, ILogger logger)
    {
        Scope = raw.Get(Oidc.Authorize.Request.Scope);
        Scopes.RequestedScopeNames.AddRange(Scope.FromSpaceSeparatedString());

        var resourceIndicators = raw.GetValues(Oidc.Authorize.Request.Resource);
        if (resourceIndicators is not null)
            Scopes.RequestedResourceUris.AddRange(resourceIndicators);

        var responseType = raw.Get(Oidc.Authorize.Request.ResponseType);
        ResponseTypes.AddRange(responseType.FromSpaceSeparatedString());
        ClientId = raw.Get(Oidc.Authorize.Request.ClientId);
        RedirectUri = raw.Get(Oidc.Authorize.Request.RedirectUri);
        State = raw.Get(Oidc.Authorize.Request.State);
        ResponseMode = raw.Get(Oidc.Authorize.Request.ResponseMode);
        Nonce = raw.Get(Oidc.Authorize.Request.Nonce);

        var display = raw.Get(Oidc.Authorize.Request.Display);
        if (display.IsPresent())
        {
            if (Options.Discovery.DisplayModeIsSupported(display))
            {
                DisplayMode = display;
            }
            else
            {
                logger.LogDebug("Unsupported display mode - ignored: {Display}", display);
            }
        }

        var prompt = raw.Get(Oidc.Authorize.Request.Prompt);
        if (prompt.IsPresent())
        {
            var prompts = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach(var p in prompts)
            {
                if (Options.Discovery.PromptModeIsSupported(p))
                {
                    PromptModes.Add(p);
                }
                else
                {
                    logger.LogDebug("Unsupported prompt mode - ignored: {Prompt}", p);
                }
            }
        }

        var maxAge = raw.Get(Oidc.Authorize.Request.MaxAge);
        if (maxAge.IsPresent())
        {
            if (int.TryParse(maxAge, out var seconds) && seconds >= 0)
            {
                MaxAge = seconds;
            }
            else
            {
                logger.LogDebug("Invalid max_age - ignored: {MaxAge}", maxAge);
            }
        }

        UiLocales = raw.Get(Oidc.Authorize.Request.UiLocales);
        IdTokenHint = raw.Get(Oidc.Authorize.Request.IdTokenHint);
        LoginHint = raw.Get(Oidc.Authorize.Request.LoginHint);

        var acrValues = raw.Get(Oidc.Authorize.Request.AcrValues);
        AcrValues.AddRange(acrValues.FromSpaceSeparatedString());

        CodeChallenge = raw.Get(Oidc.Authorize.Request.CodeChallenge);
        CodeChallengeMethod = raw.Get(Oidc.Authorize.Request.CodeChallengeMethod);
    }

    [MemberNotNull(nameof(RedirectUri))]
    public void AssertHasRedirectUri()
    {
        if (!redirectUriValidated || RedirectUri is null)
            throw new InvalidOperationException("RedirectUri was required, but is missing or invalid");
    }

    public void RedirectUriValidated() => redirectUriValidated = true;

    public void ResourcesValidated() => resourcesValidated = true;

    public void AssertResourcesValidated()
    {
        if (!resourcesValidated)
            throw new InvalidOperationException("Resources validated was required, but was not validated");
    }
}
