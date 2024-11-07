using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;

namespace RoyalIdentity.Endpoints;

public class TokenEndpoint : IEndpointHandler
{
    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        // return a problem details of a UnsupportedMediaType infoming the ContentType is invalid
        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Status = StatusCodes.Status501NotImplemented,
            Title = "Not Implemented",
            Detail = "Not Implemented yet"
        };

        return ValueTask.FromResult(
            new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails)));
    }
}
