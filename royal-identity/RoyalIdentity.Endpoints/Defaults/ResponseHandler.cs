using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Endpoints.Defaults;

/// <summary>
/// A simple implementation for when the result has already been created
/// and does not need to be processed asynchronously.
/// </summary>
/// <param name="result"></param>
public sealed class ResponseHandler(IResult result) : IResponseHandler
{
    /// <inheritdoc />
    public ValueTask<IResult> CreateResponseAsync(CancellationToken ct) => ValueTask.FromResult(result);
}
