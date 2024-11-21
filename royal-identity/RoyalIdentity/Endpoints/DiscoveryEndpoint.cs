using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Options;

namespace RoyalIdentity.Endpoints;

public class DiscoveryEndpoint : IEndpointHandler
{
    private readonly ILogger logger;
    private readonly ServerOptions options;

    public DiscoveryEndpoint(ILogger<DiscoveryEndpoint> logger, IOptions<ServerOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing discovery request.");

        // validate HTTP
        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogWarning("Discovery endpoint only supports GET requests");

            // return a problem details of a MethodNotAllowed infoming the http method is not allowed
            return new(EndpointProblemResults.MethodNotAllowed(httpContext));
        }

        logger.LogDebug("Start discovery request");

        if (!options.Endpoints.EnableDiscoveryEndpoint)
        {
            logger.LogInformation("Discovery endpoint disabled. 404.");

            // return a problem details of a NotFound informing the discovery endpoint is disabled
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = "Discovery endpoint is disabled"
            };

            return ValueTask.FromResult(
                new EndpointCreationResult(
                    httpContext,
                    ResponseHandler.Problem(problemDetails)));
        }

        var items = ContextItems.From(options);
        var context = new DiscoveryContext(httpContext, options, items);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}