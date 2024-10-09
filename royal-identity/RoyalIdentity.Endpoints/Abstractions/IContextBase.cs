
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// Base interface for all contexts
/// </summary>
public interface IContextBase
{
    /// <summary>
    /// The AspNetCore <see cref="HttpContent"/>.
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// The items created for the context can be processed.
    /// </summary>
    ContextItems Items { get; }

    /// <summary>
    /// The handler that will generate the response to the request.
    /// </summary>
    IResponseHandler? Response { get; set; }
}
