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

    public static string? GetPrefixedAcrValue(this IWithAcr context, string prefix)
    {
        var value = context.AcrValues.FirstOrDefault(x => x.StartsWith(prefix));

        if (value is not null)
            value = value.Substring(prefix.Length);


        return value;
    }

    public static void RemovePrefixedAcrValue(this IWithAcr context, string prefix)
    {
        foreach (var acr in context.AcrValues.Where(acr => acr.StartsWith(prefix, StringComparison.Ordinal)))
        {
            context.AcrValues.Remove(acr);
        }
        var acr_values = context.AcrValues.ToSpaceSeparatedString();
        if (acr_values.IsPresent())
        {
            context.Raw[OidcConstants.AuthorizeRequest.AcrValues] = acr_values;
        }
        else
        {
            context.Raw.Remove(OidcConstants.AuthorizeRequest.AcrValues);
        }
    }

    public static string? GetIdP(this IWithAcr context)
    {
        return context.GetPrefixedAcrValue(Constants.KnownAcrValues.HomeRealm);
    }

    public static void RemoveIdP(this IWithAcr context)
    {
        context.RemovePrefixedAcrValue(Constants.KnownAcrValues.HomeRealm);
    }

    public static string? GetTenant(this IWithAcr context)
    {
        return context.GetPrefixedAcrValue(Constants.KnownAcrValues.Tenant);
    }
}