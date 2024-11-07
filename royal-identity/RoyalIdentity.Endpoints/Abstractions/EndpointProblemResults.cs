
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Defaults;

namespace RoyalIdentity.Endpoints.Abstractions;

public static class EndpointProblemResults
{
    public static EndpointCreationResult MethodNotAllowed(HttpContext httpContext)
    {
        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Status = StatusCodes.Status405MethodNotAllowed,
            Title = "Method Not Allowed",
            Detail = "HTTP method is not allowed"
        };

        return new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails));
    }
}