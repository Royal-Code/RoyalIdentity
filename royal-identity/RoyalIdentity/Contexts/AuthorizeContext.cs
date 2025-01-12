using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Parameters;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace RoyalIdentity.Contexts;

public class AuthorizeContext : EndpointContextBase, IAuthorizationContextBase, IWithCodeChallenge
{
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
    /// Gets or sets the requested scopes.
    /// </summary>
    /// <value>
    /// The requested scopes.
    /// </value>
    public HashSet<string> RequestedScopes => Resources.RequestedScopes;

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
    /// The resources of the result.
    /// </summary>
    public Resources Resources { get; } = new();

    /// <summary>
    /// Gets the session id from the current user.
    /// </summary>
    public string? SessionId => Identity?.FindFirst(c => c.Type == JwtClaimTypes.SessionId)?.Value;

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
        var scope = raw.Get(OidcConstants.AuthorizeRequest.Scope);
        RequestedScopes.AddRange(scope.FromSpaceSeparatedString());

        var responseType = raw.Get(OidcConstants.AuthorizeRequest.ResponseType);
        ResponseTypes.AddRange(responseType.FromSpaceSeparatedString());
        ClientId = raw.Get(OidcConstants.AuthorizeRequest.ClientId);
        RedirectUri = raw.Get(OidcConstants.AuthorizeRequest.RedirectUri);
        State = raw.Get(OidcConstants.AuthorizeRequest.State);
        ResponseMode = raw.Get(OidcConstants.AuthorizeRequest.ResponseMode);
        Nonce = raw.Get(OidcConstants.AuthorizeRequest.Nonce);

        var display = raw.Get(OidcConstants.AuthorizeRequest.Display);
        if (display.IsPresent())
        {
            if (Constants.SupportedDisplayModes.Contains(display))
            {
                DisplayMode = display;
            }
            else
            {
                logger.LogDebug("Unsupported display mode - ignored: {Display}", display);
            }
        }

        var prompt = raw.Get(OidcConstants.AuthorizeRequest.Prompt);
        if (prompt.IsPresent())
        {
            var prompts = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (prompts.All(Constants.SupportedPromptModes.Contains))
            {
                PromptModes = prompts.ToHashSet();
            }
            else
            {
                logger.LogDebug("Unsupported prompt mode - ignored: {Promp}", prompt);
            }
        }

        var maxAge = raw.Get(OidcConstants.AuthorizeRequest.MaxAge);
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

        UiLocales = raw.Get(OidcConstants.AuthorizeRequest.UiLocales);
        IdTokenHint = raw.Get(OidcConstants.AuthorizeRequest.IdTokenHint);
        LoginHint = raw.Get(OidcConstants.AuthorizeRequest.LoginHint);

        var acrValues = raw.Get(OidcConstants.AuthorizeRequest.AcrValues);
        AcrValues.AddRange(acrValues.FromSpaceSeparatedString());

        CodeChallenge = raw.Get(OidcConstants.AuthorizeRequest.CodeChallenge);
        CodeChallengeMethod = raw.Get(OidcConstants.AuthorizeRequest.CodeChallengeMethod);
    }

#pragma warning disable CS8774

    private bool hasRedirectUri;

    [MemberNotNull(nameof(RedirectUri), nameof(ClientId))]
    public void AssertHasRedirectUri()
    {
        if (hasRedirectUri)
            return;

        hasRedirectUri = Items.Get<Asserts>()?.HasRedirectUri ?? false;
        if (!hasRedirectUri)
            throw new InvalidOperationException("RedirectUri was required, but is missing");
    }
}
