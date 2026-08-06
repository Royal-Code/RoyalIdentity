using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Pipelines.Abstractions;

/// <summary>
/// Interface for types that produce Http responses. 
/// They are produced during the processing of an endpoint context.
/// Represents the result of processing the context object.
/// </summary>
public interface IResponseHandler
{
    /// <summary>
    /// Checks whether the response represents failed processing and exposes its normalized problem details.
    /// </summary>
    /// <param name="problem">The normalized problem when the response represents a failure.</param>
    /// <returns><see langword="true"/> when the response represents a failure; otherwise, <see langword="false"/>.</returns>
    bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem);

    /// <summary>
    /// Creates the Request Response for AspNetCore.
    /// </summary>
    /// <param name="ct">The <see cref="CancellationToken"/>.</param>
    /// <returns>An <see cref="IResult"/> object of AspNetCore.</returns>
    ValueTask<IResult> CreateResponseAsync(CancellationToken ct);
}
