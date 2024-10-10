using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Options;
using System.Security.Claims;

namespace RoyalIdentity.Users.Defaults;

public class DefaultUserSession : IUserSession
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ServerOptions options;

    private bool authenticated = false;
    private ClaimsPrincipal? user;
    private AuthenticationProperties? properties;

    private HttpContext HttpContext => httpContextAccessor.HttpContext 
        ?? throw new InvalidOperationException("DefaultUserSession requires execution within a scope with the HttpContext provided by IHttpContextAccessor");

    public DefaultUserSession(IHttpContextAccessor httpContextAccessor, IOptions<ServerOptions> options)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.options = options.Value;
    }

    [Redesign("Ver definição na interface")]
    public async ValueTask<ClaimsPrincipal?> GetUserAsync()
    {
        if (!authenticated)
            await AuthenticateAsync();

        return user;
    }

    private async Task AuthenticateAsync()
    {
        var scheme = await GetCookieAuthenticationSchemeAsync();

        var handlers = HttpContext.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await handlers.GetHandlerAsync(HttpContext, scheme);
        if (handler == null)
        {
            throw new InvalidOperationException($"No authentication handler is configured to authenticate for the scheme: {scheme}");
        }

        var result = await handler.AuthenticateAsync();
        if (result is not null && result.Succeeded)
        {
            user = result.Principal;
            properties = result.Properties;
        }

        authenticated = true;
    }

    internal async Task<string> GetCookieAuthenticationSchemeAsync()
    {
        if (options.Authentication.CookieAuthenticationScheme is not null)
        {
            return options.Authentication.CookieAuthenticationScheme;
        }

        var scheme = await HttpContext.RequestServices
            .GetRequiredService<IAuthenticationSchemeProvider>()
            .GetDefaultAuthenticateSchemeAsync()
                ?? throw new InvalidOperationException(
                    "No DefaultAuthenticateScheme found or no CookieAuthenticationScheme configured on ServerOptions.");

        return scheme.Name;
    }
}
