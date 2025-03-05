using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Endpoints;

public class DiscoveryEndpoint : IEndpointHandler
{
    private readonly ILogger logger;

    public DiscoveryEndpoint(ILogger<DiscoveryEndpoint> logger)
    {
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing discovery request.");

        // validate HTTP
        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogWarning("Discovery endpoint only supports GET requests");

            // return a problem details of a MethodNotAllowed informing the http method is not allowed
            return EndpointErrorResults.MethodNotAllowed(httpContext);
        }

        logger.LogDebug("Start discovery request");

        var realmOptions = httpContext.GetRealmOptions();

        if (!realmOptions.Endpoints.EnableDiscoveryEndpoint)
        {
            logger.LogInformation("Discovery endpoint disabled. 404.");

            // return a problem details of a NotFound informing the discovery endpoint is disabled
            return EndpointErrorResults.NotFound(httpContext, "Discovery endpoint is disabled");
        }

        var serverOptions = realmOptions.ServerOptions;
        var items = ContextItems.From(serverOptions);
        var context = new DiscoveryContext(httpContext, realmOptions, items);

        return new EndpointCreationResult(context);
    }
}