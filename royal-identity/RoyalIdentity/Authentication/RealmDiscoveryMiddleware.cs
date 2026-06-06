// Ignore Spelling: app

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Authentication;

public class RealmDiscoveryMiddleware
{
    private readonly RequestDelegate next;

    public RealmDiscoveryMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // check if the request has a route value for the realm
        if (context.Request.RouteValues.TryGetValue(Server.RealmRouteKey, out var value) && value is string realm)
        {
            if (await context.SetCurrentRealmAsync(realm))
            {
                // after setting the realm, continue to the next middleware
                await next(context);
            }
            else
            {
                var dispatcher = context.RequestServices.GetService<IEventDispatcher>();
                if (dispatcher is not null)
                    await dispatcher.DispatchAsync(new RealmNotFoundEvent(realm));

                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "realm_not_found",
                    error_description = $"The realm '{realm}' was not found"
                });
            }

            return;
        }

        await next(context);
    }
}

public static class RealmAccountDiscoveryMiddlewareExtensions
{
    /// <summary>
    /// <para>
    ///     Adds the middleware to discover the realm for the mapped pipelines and account pages.
    /// </para>
    /// <para>
    ///     Must be used after routing middleware and before authentication middleware.
    /// </para>
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseRealmDiscovery(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RealmDiscoveryMiddleware>();
    }
}