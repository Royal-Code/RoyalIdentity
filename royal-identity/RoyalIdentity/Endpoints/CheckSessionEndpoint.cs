// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
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

        var options = httpContext.GetRealmOptions();
        if (!options.Endpoints.EnableCheckSessionEndpoint || !httpContext.Request.IsHttps)
        {
            logger.LogInformation(
                "Check session endpoint unavailable for realm {Realm}: enabled={Enabled}, https={Https}.",
                httpContext.GetCurrentRealm().Path,
                options.Endpoints.EnableCheckSessionEndpoint,
                httpContext.Request.IsHttps);

            return ValueTask.FromResult(EndpointErrors.NotFound(
                httpContext,
                "Check session endpoint is unavailable"));
        }

        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogDebug("Invalid HTTP method for check session endpoint");

            return ValueTask.FromResult(EndpointErrors.MethodNotAllowed(httpContext, HttpMethods.Get));
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
