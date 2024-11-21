using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
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

            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = TokenErrors.InvalidRequest,
                Detail = "Invalid HTTP request for userinfo endpoint"
            };

            return new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails));
        }

        // userinfo requires an access token on the request
        var bearerTokenResult = await bearerTokenValidator.LocatorAsync(httpContext);
        if (!bearerTokenResult.TokenFound)
        {
            logger.LogError("No access token found.");

            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = ProtectedResourceErrors.InvalidToken,
                Detail = "Invalid HTTP request for userinfo endpoint, no access token found."
            };

            return new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails));
        }

        var items = ContextItems.From(options);
        var userInfoContext = new UserInfoContext(httpContext, items, bearerTokenResult.Token);

        return new EndpointCreationResult(userInfoContext);
    }
}
