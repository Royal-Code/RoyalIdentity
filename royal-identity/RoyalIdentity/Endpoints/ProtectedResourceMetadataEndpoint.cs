using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Endpoints;

public class ProtectedResourceMetadataEndpoint : IEndpointHandler
{
    private readonly ILogger logger;

    public ProtectedResourceMetadataEndpoint(ILogger<ProtectedResourceMetadataEndpoint> logger)
    {
        this.logger = logger;
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing protected resource metadata request.");

        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogWarning("Protected resource metadata endpoint only supports GET requests");
            return ValueTask.FromResult(EndpointErrorResults.MethodNotAllowed(httpContext));
        }

        var realmOptions = httpContext.GetRealmOptions();
        if (!realmOptions.Endpoints.EnableDiscoveryEndpoint)
        {
            logger.LogInformation("Discovery endpoint disabled. 404.");
            return ValueTask.FromResult(EndpointErrorResults.NotFound(httpContext, "Discovery endpoint is disabled"));
        }

        var parameters = httpContext.Request.Query.AsNameValueCollection();
        var resourceValues = parameters.GetValues(Oidc.ProtectedResource.Metadata.Resource);
        if (resourceValues is null || resourceValues.Length is not 1 || resourceValues[0].IsMissing())
        {
            logger.LogWarning("Protected resource metadata request requires exactly one resource parameter");
            return ValueTask.FromResult(EndpointErrorResults.InvalidRequest(
                httpContext,
                "Protected resource metadata request requires exactly one resource parameter"));
        }

        var context = new ProtectedResourceMetadataContext(
            httpContext,
            parameters,
            realmOptions,
            resourceValues[0]);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}
