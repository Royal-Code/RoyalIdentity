using Microsoft.AspNetCore.Builder;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Endpoints.Mapping;

/// <summary>
/// Extension methods for map authentication endpoints.
/// </summary>
public static class DefaultServerEndpoints
{
    /// <summary>
    /// Maps an authentication server endpoint.
    /// </summary>
    /// <typeparam name="TEndpoint">The endpoint handler, responsible for creating the context object for the endpoint.</typeparam>
    /// <param name="app">The <see cref="WebApplication"/> to map endpoints.</param>
    /// <param name="pattern">The endpoint route.</param>
    /// <returns>A AspNetCore <see cref="RouteHandlerBuilder"/>.</returns>
    public static RouteHandlerBuilder MapServerEndpoint<TEndpoint>(this WebApplication app, string pattern)
    where TEndpoint : IEndpointHandler
    {
        return app.Map(pattern, ServerEndpoint<TEndpoint>.EndpointHandler);
    }
}