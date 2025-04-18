﻿// Ignore Spelling: app

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
                // if realm is not found, return 404
                // TODO: generate a proper error response and a event
                context.Response.StatusCode = 404;
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