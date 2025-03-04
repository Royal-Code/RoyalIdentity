using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
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

        return await InternalGetAuthorizationContextAsync(context, returnUrl);
    }

    public static async ValueTask<AuthorizationContext?> GetAuthorizationContextAsync(
        this HttpContext context, string? returnUrl)
    {
        // if we have a cached authorization context, return it
        if (context.Items.TryGetValue(AuthorizationContextKey, out var value) && value is AuthorizationContext ac)
        {
            return ac;
        }

        if (returnUrl.IsMissing())
            return null;

        return await InternalGetAuthorizationContextAsync(context, returnUrl);
    }

    private static async ValueTask<AuthorizationContext?> InternalGetAuthorizationContextAsync(
        HttpContext context, string returnUrl)
    {
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

    public static async ValueTask<bool> TryLoadRealmFromDomain(this HttpContext context)
    {
        // try get the returnUrl from the query string
        var domain = context.Request.Query[Constants.UIConstants.RealmPathParams.Domain].FirstOrDefault();
        if (domain.IsMissing())
            return false;

        var storate = context.RequestServices.GetRequiredService<IStorage>();
        var realm = await storate.Realms.GetByDomainAsync(domain);

        if (realm is null)
            return false;

        context.Items[Constants.RealmCurrentKey] = realm;
        context.Items[Constants.RealmRouteKey] = realm.Path;
        context.Items[Constants.RealmOptionsKey] = realm.Options;
        return true;
    }

    public static string GetRealmAuthenticationScheme(this HttpContext context)
    {
        if (context.TryGetCurrentRealm(out var realm))
            return $"{Constants.RealmAuthenticationNamePrefix}{realm.Path}";

        var realmPath = context.GetRealmPath();
        if (realmPath is not null)
            return $"{Constants.RealmAuthenticationNamePrefix}{realmPath}";

        throw new InvalidOperationException("Realm is not available. Use the middleware 'UseRealmDiscovery' to set the current realm.");
    }

    internal static TimeProvider GetClock(this HttpContext context)
    {
        return context.RequestServices.GetRequiredService<TimeProvider>();
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