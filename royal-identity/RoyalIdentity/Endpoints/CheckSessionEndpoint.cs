using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Responses;

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
            logger.LogDebug("Invalid HTTP method for check session endpoint");

            return ValueTask.FromResult(EndpointErrorResults.MethodNotAllowed(httpContext));
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
