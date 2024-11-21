using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Endpoints;

public class AuthorizeCallbackEndpoint : IEndpointHandler
{
    private readonly ServerOptions options;
    private readonly ILogger logger;
    private readonly IAuthorizeParametersStore? authorizationParametersStore;

    public AuthorizeCallbackEndpoint(
        IOptions<ServerOptions> options,
        ILogger<AuthorizeCallbackEndpoint> logger,
        IAuthorizeParametersStore? authorizationParametersStore = null)
    {
        this.options = options.Value;
        this.logger = logger;
        this.authorizationParametersStore = authorizationParametersStore;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing Authorize Callback request.");

        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP method for authorize endpoint.");

            // return a problem details of a MethodNotAllowed infoming the http method is not allowed
            return EndpointProblemResults.MethodNotAllowed(httpContext);
        }

        logger.LogDebug("Start authorize callback request");

        var parameters = httpContext.Request.Query.AsNameValueCollection();
        if (authorizationParametersStore is not null)
        {
            var messageStoreId = parameters[Constants.AuthorizationParamsStore.MessageStoreIdParameterName];
            if (messageStoreId is not null)
            {
                parameters = await authorizationParametersStore.ReadAsync(messageStoreId, httpContext.RequestAborted);
                await authorizationParametersStore.DeleteAsync(messageStoreId, httpContext.RequestAborted);
            }
        }

        if (parameters is null)
        {
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = OidcConstants.AuthorizeErrors.InvalidRequest,
                Detail = "Invalid parameters"
            };

            return new EndpointCreationResult(
                    httpContext,
                    ResponseHandler.Problem(problemDetails));
        }

        var user = httpContext.User;

        if (user is null)
        {
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = OidcConstants.AuthorizeErrors.LoginRequired,
                Detail = "Login required"
            };

            return new EndpointCreationResult(
                    httpContext,
                    ResponseHandler.Problem(problemDetails));
        }

        var items = ContextItems.From(options);
        var context = new AuthorizeContext(httpContext, parameters, user, items);

        context.Load(logger);

        return new EndpointCreationResult(context);
    }
}
