// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;
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
            return EndpointErrors.MethodNotAllowed(httpContext, HttpMethods.Get);
        }

        logger.LogDebug("Start discovery request");

        var realmOptions = httpContext.GetRealmOptions();

        if (!realmOptions.Endpoints.EnableDiscoveryEndpoint)
        {
            logger.LogInformation("Discovery endpoint disabled. 404.");

            // return a problem details of a NotFound informing the discovery endpoint is disabled
            return EndpointErrors.NotFound(httpContext, "Discovery endpoint is disabled");
        }

        var context = new DiscoveryContext(httpContext, realmOptions);

        return new EndpointCreationResult(context);
    }
}
