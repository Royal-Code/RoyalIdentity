// Ignore Spelling: Jwk

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Endpoints;

public class JwkEndpoint : IEndpointHandler
{
    private readonly ILogger logger;

    public JwkEndpoint(ILogger<JwkEndpoint> logger)
    {
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing jwk discovery request.");

        // validate HTTP
        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogWarning("JWK Discovery endpoint only supports GET requests");

            // return a problem details of a MethodNotAllowed informing the http method is not allowed
            return EndpointErrorResults.MethodNotAllowed(httpContext);
        }

        logger.LogDebug("Start jwk discovery request");

        var realmOptions = httpContext.GetRealmOptions();

        if (!realmOptions.Discovery.ShowKeySet)
        {
            logger.LogDebug("JWK Discovery endpoint disabled. 404.");

            return EndpointErrorResults.NotFound(httpContext, "Discovery endpoint is disabled");
        }

        var serverOptions = realmOptions.ServerOptions;
        var items = ContextItems.From(serverOptions);
        var context = new JwkContext(httpContext, items);

        return new EndpointCreationResult(context);
    }
}
