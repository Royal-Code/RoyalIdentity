using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Options;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Extension methods for signin/out using the IdentityServer authentication scheme.
/// </summary>
public static class AuthenticationExtensions
{
    // /// <summary>
    // /// Signs the user in.
    // /// </summary>
    // /// <param name="context">The manager.</param>
    // /// <param name="user">The IdentityServer user.</param>
    // /// <returns></returns>
    // public static async Task SignInAsync(this HttpContext context, IdentityServerUser user)
    // {
    //     await context.SignInAsync(await context.GetCookieAuthenticationSchemeAsync(), user.CreatePrincipal());
    // }

    // /// <summary>
    // /// Signs the user in.
    // /// </summary>
    // /// <param name="context">The manager.</param>
    // /// <param name="user">The IdentityServer user.</param>
    // /// <param name="properties">The authentication properties.</param>
    // /// <returns></returns>
    // public static async Task SignInAsync(this HttpContext context, IdentityServerUser user,
    //     AuthenticationProperties properties)
    // {
    //     await context.SignInAsync(await context.GetCookieAuthenticationSchemeAsync(), user.CreatePrincipal(),
    //         properties);
    // }

    internal static TimeProvider GetClock(this HttpContext context)
    {
        return context.RequestServices.GetRequiredService<TimeProvider>();
    }

    internal static ValueTask<string> GetCookieAuthenticationSchemeAsync(this HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value;
        return context.GetCookieAuthenticationSchemeAsync(options);
    }

    internal static async ValueTask<string> GetCookieAuthenticationSchemeAsync(this HttpContext context, ServerOptions options)
    {
        if (options.Authentication.CookieAuthenticationScheme is not null)
        {
            return options.Authentication.CookieAuthenticationScheme;
        }

        var scheme = await context.RequestServices
            .GetRequiredService<IAuthenticationSchemeProvider>()
            .GetDefaultAuthenticateSchemeAsync()
                ?? throw new InvalidOperationException(
                    "No DefaultAuthenticateScheme found or no CookieAuthenticationScheme configured on ServerOptions.");

        return scheme.Name;
    }

    public static bool IsValidReturnUrl(this string returnUrl)
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