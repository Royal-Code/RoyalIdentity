using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Endpoints;

public class JwkEndpoint : IEndpointHandler
{
    private readonly ILogger logger;
    private readonly ServerOptions options;

    public JwkEndpoint(ILogger<JwkEndpoint> logger, IOptions<ServerOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing jwk discovery request.");

        // validate HTTP
        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogWarning("JWK Discovery endpoint only supports GET requests");

            // return a problem details of a MethodNotAllowed infoming the http method is not allowed
            return new(EndpointErrorResults.MethodNotAllowed(httpContext));
        }

        logger.LogDebug("Start jwk discovery request");

        if (!options.Discovery.ShowKeySet)
        {
            logger.LogDebug("JWK Discovery endpoint disabled. 404.");

            return new(EndpointErrorResults.NotFound(httpContext, "Discovery endpoint is disabled"));
        }

        var items = ContextItems.From(options);
        var context = new JwkContext(httpContext, options, items);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}
