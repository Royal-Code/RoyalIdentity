using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using static RoyalIdentity.Options.OidcConstants;

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

            return EndpointErrorResults.MethodNotAllowed(httpContext);
        }

        // user info requires an access token on the request
        var bearerTokenResult = await bearerTokenLocator.LocateAsync(httpContext);
        if (!bearerTokenResult.TokenFound)
        {
            logger.LogError("No access token found.");

            return EndpointErrorResults.BadRequest(
                httpContext,
                ProtectedResourceErrors.InvalidToken,
                "Invalid HTTP request for user info endpoint, no access token found.");
        }

        var options = httpContext.GetCurrentRealm().Options.ServerOptions;
        var items = ContextItems.From(options);
        var userInfoContext = new UserInfoContext(httpContext, items, bearerTokenResult.Token);

        return new EndpointCreationResult(userInfoContext);
    }
}
