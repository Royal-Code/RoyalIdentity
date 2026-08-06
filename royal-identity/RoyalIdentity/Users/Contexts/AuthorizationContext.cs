using RoyalIdentity.Contexts;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using System.Collections.Specialized;
using System.Security.Claims;

namespace RoyalIdentity.Users.Contexts;

public class AuthorizationContext
{
    public AuthorizationContext(AuthorizeValidateContext context)
    {
        Client = context.ClientParameters.Client!;
        User = context.Subject!;
        RedirectUri = context.RedirectUri!;
        DisplayMode = context.DisplayMode;
        UiLocales = context.UiLocales;
        LoginHint = context.LoginHint;
        PromptModes = context.PromptModes;
        AcrValues = context.AcrValues;
        Resources = context.Scopes;
        Parameters = context.Raw;
        RequestObjectValues = null;
    }

    /// <summary>
    /// Gets the entire parameter collection.
    /// </summary>
    /// <value>
    /// The parameters.
    /// </value>
    public NameValueCollection Parameters { get; }

    /// <summary>
    /// The client.
    /// </summary>
    public Client Client { get; }

    /// <summary>
    /// The user.
    /// </summary>
    public ClaimsPrincipal User { get; set; }

    /// <summary>
    /// Gets or sets the redirect URI.
    /// </summary>
    /// <value>
    /// The redirect URI.
    /// </value>
    public string RedirectUri { get; }

    /// <summary>
    /// The display mode passed from the authorization request.
    /// </summary>
    /// <value>
    /// The display mode.
    /// </value>
    public string? DisplayMode { get; }

    /// <summary>
    /// The UI locales passed from the authorization request.
    /// </summary>
    /// <value>
    /// The UI locales.
    /// </value>
    public string? UiLocales { get; }

    /// <summary>
    /// The expected user name the user will use to login. This is requested from the client 
    /// via the <c>login_hint</c> parameter on the authorize request.
    /// </summary>
    /// <value>
    /// The LoginHint.
    /// </value>
    public string? LoginHint { get; }

    /// <summary>
    /// Gets or sets the collection of prompt modes.
    /// </summary>
    /// <value>
    /// The collection of prompt modes.
    /// </value>
    public HashSet<string> PromptModes { get; }

    /// <summary>
    /// The validated resources.
    /// </summary>
    public RequestedResources Resources { get; }

    /// <summary>
    /// The ordered ACR preferences passed from the authorization request.
    /// </summary>
    /// <value>
    /// The distinct preferences in client order.
    /// </value>
    public IReadOnlyList<string> AcrValues { get; }

    /// <summary>
    /// Gets the validated contents of the request object (if present)
    /// </summary>
    /// <value>
    /// The request object values
    /// </value>
    public Dictionary<string, string>? RequestObjectValues { get; }
}
