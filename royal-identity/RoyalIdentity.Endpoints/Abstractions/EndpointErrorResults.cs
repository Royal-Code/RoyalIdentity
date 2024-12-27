using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Defaults;

namespace RoyalIdentity.Endpoints.Abstractions;

public static class EndpointErrorResults
{
    public static EndpointCreationResult MethodNotAllowed(HttpContext httpContext)
    {
        return new EndpointCreationResult(
            httpContext,
            ResponseHandler.Error(
                "method_not_allowed", 
                "The HTTP method is not allowed", 
                statusCode: StatusCodes.Status405MethodNotAllowed));
    }

    public static EndpointCreationResult UnsupportedMediaType(HttpContext httpContext)
    {
        return new EndpointCreationResult(
            httpContext,
            ResponseHandler.Error(
                "Invalid_content_type",
                "The content type must be: application/x-www-form-urlencoded",
                statusCode: StatusCodes.Status415UnsupportedMediaType));
    }

    public static EndpointCreationResult NotFound(HttpContext httpContext, string? description)
    {
        return new EndpointCreationResult(
            httpContext,
            ResponseHandler.Error(
                "not_found",
                description,
                statusCode: StatusCodes.Status404NotFound));
    }

    public static EndpointCreationResult BadRequest(HttpContext httpContext, string error, string? description)
    {
        return new EndpointCreationResult(
            httpContext,
            ResponseHandler.Error(error, description, statusCode: StatusCodes.Status400BadRequest));
    }

    public static EndpointCreationResult InvalidRequest(HttpContext httpContext, string? description)
    {
        return BadRequest(httpContext, "invalid_request", description);
    }
}