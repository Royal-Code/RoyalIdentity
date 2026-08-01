// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Endpoints;

public class UserInfoEndpoint : IEndpointHandler
{
    private readonly IBearerTokenLocator bearerTokenLocator;
    private readonly ILogger logger;

    public UserInfoEndpoint(
        IBearerTokenLocator bearerTokenLocator,
        ILogger<UserInfoEndpoint> logger)
    {
        this.bearerTokenLocator = bearerTokenLocator;
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing User Info request.");

        if (!HttpMethods.IsGet(httpContext.Request.Method) && !HttpMethods.IsPost(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP method for user info endpoint.");

            return EndpointErrors.MethodNotAllowed(httpContext, HttpMethods.Get, HttpMethods.Post);
        }

        // user info requires an access token on the request
        var bearerTokenResult = await bearerTokenLocator.LocateAsync(httpContext);
        if (!bearerTokenResult.TokenFound)
        {
            logger.LogError("No access token found.");

            return EndpointErrorResults.BadRequest(
                httpContext,
                Oidc.ProtectedResource.Errors.InvalidToken,
                "Invalid HTTP request for user info endpoint, no access token found.");
        }

        var items = new ContextItems();
        var userInfoContext = new UserInfoContext(httpContext, items, bearerTokenResult.Token);

        return new EndpointCreationResult(userInfoContext);
    }
}
