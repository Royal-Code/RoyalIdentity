
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// <para>
///     Represents a service that analyses input from an authentication server endpoint 
///     and creates a context object to process the request.
/// </para>
/// </summary>
public interface IEndpointHandler
{
    /// <summary>
    /// <para>
    ///     It processes the input from an endpoint and creates a context object for the request to be processed.
    /// </para>
    /// </summary>
    /// <param name="httpContext">The AspNetCore <see cref="HttpContext"/></param>
    /// <returns>
    ///     The result of creating the context object for the endpoint.
    /// </returns>
    ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext);
}
