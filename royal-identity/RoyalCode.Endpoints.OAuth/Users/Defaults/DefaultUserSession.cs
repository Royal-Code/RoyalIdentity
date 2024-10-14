using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Options;
using System.Security.Claims;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Users.Defaults;

public class DefaultUserSession : IUserSession
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ServerOptions options;

    private bool authenticated = false;
    private ClaimsPrincipal? user;
    private AuthenticationProperties? properties;

    private HttpContext HttpContext => httpContextAccessor.HttpContext
                                       ?? throw new InvalidOperationException(
                                           "DefaultUserSession requires execution within a scope with the HttpContext provided by IHttpContextAccessor");

    public DefaultUserSession(IHttpContextAccessor httpContextAccessor, IOptions<ServerOptions> options)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.options = options.Value;
    }

    /// <summary>
    /// Adds a client to the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">clientId</exception>
    public virtual async Task AddClientIdAsync(string clientId)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        await AuthenticateAsync();
        if (properties is null)
            return;

        var clientIds = properties.GetClientList();
        if (!clientIds.Contains(clientId))
        {
            properties.AddClientId(clientId);
            await UpdateSessionCookie();
        }
    }

    [Redesign("Ver definição na interface")]
    public async ValueTask<ClaimsPrincipal?> GetUserAsync()
    {
        if (!authenticated)
            await AuthenticateAsync();

        return user;
    }

    private async ValueTask AuthenticateAsync()
    {
        if (authenticated)
            return;

        var scheme = await HttpContext.GetCookieAuthenticationSchemeAsync(options);

        var handlers = HttpContext.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        var handler = await handlers.GetHandlerAsync(HttpContext, scheme);
        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No authentication handler is configured to authenticate for the scheme: {scheme}");
        }

        var result = await handler.AuthenticateAsync();
        if (result.Succeeded)
        {
            user = result.Principal;
            properties = result.Properties;
        }

        authenticated = true;
    }

    // client list helpers
    private async Task UpdateSessionCookie()
    {
        await AuthenticateAsync();

        if (user == null || properties == null)
            throw new InvalidOperationException("User is not currently authenticated");

        var scheme = await HttpContext.GetCookieAuthenticationSchemeAsync();
        await HttpContext.SignInAsync(scheme, user, properties);
    }
}