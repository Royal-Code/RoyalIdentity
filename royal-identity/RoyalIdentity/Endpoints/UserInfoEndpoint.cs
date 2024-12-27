using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Options;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Endpoints;

public class UserInfoEndpoint : IEndpointHandler
{
    private readonly IBearerTokenLocator bearerTokenValidator;
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public UserInfoEndpoint(
        IBearerTokenLocator bearerTokenValidator,
        IOptions<ServerOptions> options,
        ILogger<UserInfoEndpoint> logger)
    {
        this.bearerTokenValidator = bearerTokenValidator;
        this.options = options.Value;
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing User Info request.");

        if (!HttpMethods.IsGet(httpContext.Request.Method) && !HttpMethods.IsPost(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP method for userinfo endpoint.");

            return EndpointErrorResults.MethodNotAllowed(httpContext);
        }

        // userinfo requires an access token on the request
        var bearerTokenResult = await bearerTokenValidator.LocatorAsync(httpContext);
        if (!bearerTokenResult.TokenFound)
        {
            logger.LogError("No access token found.");

            return EndpointErrorResults.BadRequest(
                httpContext,
                ProtectedResourceErrors.InvalidToken,
                "Invalid HTTP request for userinfo endpoint, no access token found.");
        }

        var items = ContextItems.From(options);
        var userInfoContext = new UserInfoContext(httpContext, items, bearerTokenResult.Token);

        return new EndpointCreationResult(userInfoContext);
    }
}
