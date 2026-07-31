using Microsoft.AspNetCore.Http;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Endpoints;

/// <summary>
/// Endpoint failures that happen before a typed context exists and whose code is selected here, in the core,
/// and never in the protocol-neutral <c>RoyalIdentity.Pipelines</c> (DF19).
/// </summary>
/// <remarks>
/// These three failures are HTTP-level: they occur before the request qualifies as a protocol request, so they
/// are not part of the OAuth error taxonomy. Failures that do select an OAuth code call
/// <see cref="EndpointErrorResults.BadRequest"/> directly with the matching <c>Oidc.*.Errors.*</c> constant,
/// so the chosen code stays visible at the call site.
/// </remarks>
public static class EndpointErrors
{
    // Not OAuth error codes: they name an HTTP condition. DF12 revisits whether they should carry a code at
    // all when the 405 response gains its Allow header.
    private const string MethodNotAllowedCode = "method_not_allowed";
    private const string InvalidContentTypeCode = "invalid_content_type";
    private const string NotFoundCode = "not_found";

    public static EndpointCreationResult MethodNotAllowed(HttpContext httpContext)
    {
        return EndpointErrorResults.Error(
            httpContext,
            MethodNotAllowedCode,
            "The HTTP method is not allowed",
            StatusCodes.Status405MethodNotAllowed);
    }

    public static EndpointCreationResult UnsupportedMediaType(HttpContext httpContext)
    {
        return EndpointErrorResults.Error(
            httpContext,
            InvalidContentTypeCode,
            "The content type must be: application/x-www-form-urlencoded",
            StatusCodes.Status415UnsupportedMediaType);
    }

    public static EndpointCreationResult NotFound(HttpContext httpContext, string? description)
    {
        return EndpointErrorResults.Error(
            httpContext,
            NotFoundCode,
            description,
            StatusCodes.Status404NotFound);
    }
}
