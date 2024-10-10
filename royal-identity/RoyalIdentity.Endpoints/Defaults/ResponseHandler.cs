using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;
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
        => new(Results.Json(problemDetails, statusCode: problemDetails.Status ?? 400));

    /// <inheritdoc />
    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct) => ValueTask.FromResult(result);
}
