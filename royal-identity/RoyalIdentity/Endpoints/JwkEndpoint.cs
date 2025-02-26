// Ignore Spelling: Jwk

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
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

        var realmOptions = await httpContext.GetRealmOptionsAsync();

        if (!realmOptions.Discovery.ShowKeySet)
        {
            logger.LogDebug("JWK Discovery endpoint disabled. 404.");

            return EndpointErrorResults.NotFound(httpContext, "Discovery endpoint is disabled");
        }

        var items = ContextItems.From(options);
        var context = new JwkContext(httpContext, realmOptions, items);

        return new EndpointCreationResult(context);
    }
}
