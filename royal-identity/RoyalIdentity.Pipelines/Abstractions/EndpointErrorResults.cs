using Microsoft.AspNetCore.Http;
using RoyalIdentity.Pipelines.Defaults;

namespace RoyalIdentity.Pipelines.Abstractions;

/// <summary>
/// Builds an <see cref="EndpointCreationResult"/> that fails before a typed context exists.
/// </summary>
/// <remarks>
/// Every factory here receives the error code, the status and the headers from the caller. This project stays
/// neutral to the protocol: it must not know, choose or hardcode any OAuth/OIDC error code. The factories that
/// select those codes live in <c>RoyalIdentity</c>.
/// </remarks>
public static class EndpointErrorResults
{
    /// <summary>
    /// Fails endpoint creation with the code, status and headers chosen by the caller.
    /// </summary>
    public static EndpointCreationResult Error(
        HttpContext httpContext,
        string error,
        string? description = null,
        int statusCode = StatusCodes.Status400BadRequest,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return new EndpointCreationResult(
            httpContext,
            ResponseHandler.Error(error, description, statusCode: statusCode, headers: headers));
    }

    /// <summary>
    /// Fails endpoint creation with HTTP 400 and the code chosen by the caller.
    /// </summary>
    public static EndpointCreationResult BadRequest(HttpContext httpContext, string error, string? description)
    {
        return Error(httpContext, error, description, StatusCodes.Status400BadRequest);
    }
}
