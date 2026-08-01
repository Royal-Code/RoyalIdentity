using Microsoft.AspNetCore.Http;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses.HttpResults;

namespace RoyalIdentity.Endpoints;

/// <summary>
/// Endpoint failures that happen before a typed context exists and whose classification is selected here, in
/// the core, and never in the protocol-neutral <c>RoyalIdentity.Pipelines</c> (DF19).
/// </summary>
/// <remarks>
/// These three failures belong to HTTP, not to the OAuth taxonomy: the request never became a protocol request.
/// They answer <c>application/problem+json</c> rather than an OAuth error envelope carrying an invented code
/// (DF12). Failures that do select an OAuth code call <see cref="EndpointErrorResults.BadRequest"/> directly
/// with the matching <c>Oidc.*.Errors.*</c> constant, so the chosen code stays visible at the call site.
/// </remarks>
public static class EndpointErrors
{
    /// <summary>
    /// HTTP 405 with the <c>Allow</c> header RFC 9110 §15.5.6 requires.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="allowedMethods">The methods this endpoint serves, in the order they should be announced.</param>
    public static EndpointCreationResult MethodNotAllowed(
        HttpContext httpContext,
        params string[] allowedMethods)
    {
        ArgumentOutOfRangeException.ThrowIfZero(allowedMethods.Length);

        return new EndpointCreationResult(
            httpContext,
            new HttpFailureResult(
                StatusCodes.Status405MethodNotAllowed,
                "Method not allowed",
                "The HTTP method is not allowed for this endpoint",
                new Dictionary<string, string> { ["Allow"] = string.Join(", ", allowedMethods) }));
    }

    public static EndpointCreationResult UnsupportedMediaType(HttpContext httpContext)
    {
        return new EndpointCreationResult(
            httpContext,
            new HttpFailureResult(
                StatusCodes.Status415UnsupportedMediaType,
                "Unsupported media type",
                "The content type must be: application/x-www-form-urlencoded"));
    }

    public static EndpointCreationResult NotFound(HttpContext httpContext, string? description)
    {
        return new EndpointCreationResult(
            httpContext,
            new HttpFailureResult(
                StatusCodes.Status404NotFound,
                "Not found",
                description));
    }
}
