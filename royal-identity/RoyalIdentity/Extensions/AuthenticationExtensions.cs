using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Options;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contexts;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Extension methods for sign in/out using the IdentityServer authentication scheme.
/// </summary>
public static class AuthenticationExtensions
{
    private static readonly string AuthorizationContextKey = typeof(AuthorizationContext).FullName!;

    public static async ValueTask<AuthorizationContext?> GetAuthorizationContextAsync(this HttpContext context)
    {
        // if we have a cached authorization context, return it
        if (context.Items.TryGetValue(AuthorizationContextKey, out var value) && value is AuthorizationContext ac)
        {
            return ac;
        }

        // try get the returnUrl from the query string
        var returnUrl = context.Request.Query[Constants.UIConstants.DefaultRoutePathParams.Login].FirstOrDefault();
        if (returnUrl.IsMissing())
            return null;

        var signInManager = context.RequestServices.GetRequiredService<ISignInManager>();
        var authorizationContext = await signInManager.GetAuthorizationContextAsync(returnUrl, context.RequestAborted);
        if (authorizationContext is not null)
        {
            context.Items[AuthorizationContextKey] = authorizationContext;
            var realm = authorizationContext.Client.Realm;
            context.Items[Constants.RealmCurrentKey] = realm;
            context.Items[Constants.RealmRouteKey] = realm.Path;
            context.Items[Constants.RealmOptionsKey] = realm.Options;
        }

        return authorizationContext;
    }

    internal static TimeProvider GetClock(this HttpContext context)
    {
        return context.RequestServices.GetRequiredService<TimeProvider>();
    }

    [Obsolete("Realm required - must use the realm scheme")]
    internal static ValueTask<string> GetCookieAuthenticationSchemeAsync(this HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value;
        return context.GetCookieAuthenticationSchemeAsync(options);
    }

    internal static async ValueTask<string> GetCookieAuthenticationSchemeAsync(this HttpContext context, ServerOptions options)
    {
        // remove this when we have realms
        ////if (options.Authentication.CookieAuthenticationScheme is not null)
        ////{
        ////    return options.Authentication.CookieAuthenticationScheme;
        ////}

        var scheme = await context.RequestServices
            .GetRequiredService<IAuthenticationSchemeProvider>()
            .GetDefaultAuthenticateSchemeAsync()
                ?? throw new InvalidOperationException(
                    "No DefaultAuthenticateScheme found or no CookieAuthenticationScheme configured on ServerOptions.");

        return scheme.Name;
    }

    public static bool IsValidReturnUrl([NotNullWhen(true)] this string? returnUrl)
    {
        if (returnUrl.IsLocalUrl())
        {
            var index = returnUrl.IndexOf('?');
            if (index >= 0)
            {
                returnUrl = returnUrl.Substring(0, index);
            }

            if (returnUrl.EndsWith(Constants.ProtocolRoutePaths.Authorize, StringComparison.Ordinal) ||
                returnUrl.EndsWith(Constants.ProtocolRoutePaths.AuthorizeCallback, StringComparison.Ordinal))
            {

                return true;
            }
        }

        return false;
    }
}