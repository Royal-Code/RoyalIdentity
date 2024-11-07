using RoyalIdentity.Contexts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using System.Collections.Specialized;

namespace RoyalIdentity.Users.Contexts;

public class AuthorizationContext
{
    public AuthorizationContext(AuthorizeValidateContext context)
    {
        Client = context.Client!;
        RedirectUri = context.RedirectUri!;
        DisplayMode = context.DisplayMode;
        UiLocales = context.UiLocales;
        IdP = context.GetIdP();
        Tenant = context.GetTenant();
        LoginHint = context.LoginHint;
        PromptModes = context.PromptModes;
        AcrValues = context.AcrValues;
        Resources = context.Resources;
        Parameters = context.Raw;
        RequiredConsent = context.RequiredConsent;

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
    /// Gets if consent is required.
    /// </summary>
    /// <value>
    /// Is consent required?
    /// </value>
    public bool RequiredConsent { get; }

    /// <summary>
    /// The client.
    /// </summary>
    public Client Client { get; }

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
    /// The expected username the user will use to login. This is requested from the client 
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
    public Resources Resources { get; }

    /// <summary>
    /// The acr values passed from the authorization request.
    /// </summary>
    /// <value>
    /// The acr values.
    /// </value>
    public HashSet<string> AcrValues { get; }

    /// <summary>
    /// The external identity provider requested. This is used to bypass home realm 
    /// discovery (HRD). This is provided via the <c>"idp:"</c> prefix to the <c>acr</c> 
    /// parameter on the authorize request.
    /// </summary>
    /// <value>
    /// The external identity provider identifier.
    /// </value>
    public string? IdP { get; }

    /// <summary>
    /// The tenant requested. This is provided via the <c>"tenant:"</c> prefix to 
    /// the <c>acr</c> parameter on the authorize request.
    /// </summary>
    /// <value>
    /// The tenant.
    /// </value>
    public string? Tenant { get; }

    /// <summary>
    /// Gets the validated contents of the request object (if present)
    /// </summary>
    /// <value>
    /// The request object values
    /// </value>
    public Dictionary<string, string>? RequestObjectValues { get; }
}
