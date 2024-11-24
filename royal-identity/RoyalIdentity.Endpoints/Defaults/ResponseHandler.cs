using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace RoyalIdentity.Endpoints.Defaults;

/// <summary>
/// A simple implementation for when the result has already been created
/// and does not need to be processed asynchronously.
/// </summary>
/// <param name="result"></param>
public sealed class ResponseHandler(IResult result) : IResponseHandler
{
    public static ResponseHandler Create(HttpStatusCode statusCode, object value)
        => new(Results.Json(value, statusCode: (int)statusCode));

    public static ResponseHandler Problem(ProblemDetails problemDetails)
        => new(Results.Json(problemDetails.IncludeErrorsProperties(), statusCode: problemDetails.Status ?? 400));

    public static ResponseHandler Ok() => new(Results.Ok());

    public static ResponseHandler Redirect(string url) => new(Results.Redirect(url));

    /// <inheritdoc />
    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct) => ValueTask.FromResult(result);

    public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
    {
        problem = result is IValueHttpResult<ProblemDetails> valueResult 
            ? valueResult.Value
            : null;

        return problem is not null;
    }
}
