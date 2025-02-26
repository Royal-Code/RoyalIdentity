using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
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

        var realmOptions = await httpContext.GetRealmOptionsAsync();

        if (!realmOptions.Endpoints.EnableDiscoveryEndpoint)
        {
            logger.LogInformation("Discovery endpoint disabled. 404.");

            // return a problem details of a NotFound informing the discovery endpoint is disabled
            return EndpointErrorResults.NotFound(httpContext, "Discovery endpoint is disabled");
        }

        var items = ContextItems.From(options);
        var context = new DiscoveryContext(httpContext, realmOptions, items);

        return new EndpointCreationResult(context);
    }
}