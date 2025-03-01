using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Endpoints;

public class AuthorizeCallbackEndpoint : IEndpointHandler
{
    private readonly IStorage storage;
    private readonly ILogger logger;

    public AuthorizeCallbackEndpoint(
        IStorage storage,
        ILogger<AuthorizeCallbackEndpoint> logger)
    {
        this.storage = storage;
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing Authorize Callback request.");

        if (!HttpMethods.IsGet(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP method for authorize endpoint.");

            // return a problem details of a MethodNotAllowed informing the http method is not allowed
            return EndpointErrorResults.MethodNotAllowed(httpContext);
        }

        logger.LogDebug("Start authorize callback request");

        var realm = httpContext.GetCurrentRealm();

        var parameters = httpContext.Request.Query.AsNameValueCollection();
        if (realm.Options.StoreAuthorizationParameters)
        {
            var messageStoreId = parameters[Constants.AuthorizationParamsStore.MessageStoreIdParameterName];
            if (messageStoreId is not null)
            {
                parameters = await storage.AuthorizeParameters.ReadAsync(messageStoreId, httpContext.RequestAborted);
                await storage.AuthorizeParameters.DeleteAsync(messageStoreId, httpContext.RequestAborted);
            }
        }

        if (parameters is null)
            return EndpointErrorResults.InvalidRequest(httpContext, "Invalid parameters");

        var user = httpContext.User;

        if (user is null)
        {
            return EndpointErrorResults.BadRequest(httpContext, AuthorizeErrors.LoginRequired, "Login required");
        }

        var items = ContextItems.From(realm.Options.ServerOptions);
        var context = new AuthorizeContext(httpContext, parameters, user, items);

        context.Load(logger);

        return new EndpointCreationResult(context);
    }
}
