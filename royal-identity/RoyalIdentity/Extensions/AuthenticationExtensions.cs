using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Options;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Extension methods for signin/out using the IdentityServer authentication scheme.
/// </summary>
public static class AuthenticationExtensions
{
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