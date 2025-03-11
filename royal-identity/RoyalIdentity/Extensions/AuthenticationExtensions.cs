using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
        var returnUrl = context.Request.Query[UI.Routes.Params.ReturnUrl].FirstOrDefault();
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
        }

        return authorizationContext;
    }

    public static string GetRealmAuthenticationScheme(this HttpContext context)
    {
        if (context.TryGetCurrentRealm(out var realm))
            return $"{Server.RealmAuthenticationNamePrefix}{realm.Path}";

        var realmPath = context.GetRealmPath();
        if (realmPath is not null)
            return $"{Server.RealmAuthenticationNamePrefix}{realmPath}";

        throw new InvalidOperationException("Realm is not available. Use the middleware 'UseRealmDiscovery' to set the current realm.");
    }
   
    public static bool IsValidReturnUrl([NotNullWhen(true)] this string? returnUrl, string realm)
    {
        if (returnUrl.IsLocalUrl())
        {
            var index = returnUrl.IndexOf('?');
            if (index >= 0)
            {
                returnUrl = returnUrl[..index];
            }

            var authorizePath = Oidc.Routes.BuildAuthorizeUrl(realm);
            if (returnUrl.EndsWith(authorizePath, StringComparison.Ordinal))
            {
                return true;
            }

            var authorizeCallbackPath = Oidc.Routes.BuildAuthorizeCallbackUrl(realm);
            if (returnUrl.EndsWith(authorizeCallbackPath, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}