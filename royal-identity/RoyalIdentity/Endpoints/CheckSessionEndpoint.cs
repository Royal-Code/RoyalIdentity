using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Responses;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Endpoints;

public class CheckSessionEndpoint : IEndpointHandler
{
    private readonly ILogger logger;

    public CheckSessionEndpoint(ILogger<CheckSessionEndpoint> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing CheckSession request.");

        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP method for check session endpoint");

            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status405MethodNotAllowed,
                Title = TokenErrors.InvalidRequest,
                Detail = "Invalid HTTP request for token endpoint"
            };

            return ValueTask.FromResult(new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails)));
        }
        else
        {
            logger.LogDebug("Rendering check session result");

            return ValueTask.FromResult(new EndpointCreationResult(
                httpContext,
                CheckSessionResponse.Instance));
        }
    }
}
