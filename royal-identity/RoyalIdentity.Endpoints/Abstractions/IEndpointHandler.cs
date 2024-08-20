
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// <para>
///     Represents a service that analyses input from an authentication server endpoint 
///     and creates a context object to process the request.
/// </para>
/// </summary>
/// <typeparam name="TContext">Type of context object.</typeparam>
public interface IEndpointHandler
{
    /// <summary>
    /// <para>
    ///     It processes the input from an endpoint and creates a context object for the request to be processed.
    /// </para>
    /// </summary>
    /// <param name="httpContext">The AspNetCore <see cref="HttpContext"/></param>
    /// <param name="context">The endpoint context object.</param>
    /// <returns>
    ///     True if it was possible to create the context object for the endpoint, false if there is a problem with the entries.
    /// </returns>
    ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext);
}
