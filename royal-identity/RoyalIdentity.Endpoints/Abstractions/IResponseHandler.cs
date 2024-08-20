
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// Interface for types that produce Http responses. 
/// They are produced during the processing of an endpoint context.
/// Represents the result of processing the context object.
/// </summary>
public interface IResponseHandler
{
    /// <summary>
    /// Creates the Request Response for AspNetCore.
    /// </summary>
    /// <param name="ct">The <see cref="CancellationToken"/>.</param>
    /// <returns>An <see cref="IResult"/> object of AspNetCore.</returns>
    ValueTask<IResult> CreateResponseAsync(CancellationToken ct);
}