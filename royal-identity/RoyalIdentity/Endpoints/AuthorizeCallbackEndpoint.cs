// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;

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
            return EndpointErrors.MethodNotAllowed(httpContext, HttpMethods.Get);
        }

        logger.LogDebug("Start authorize callback request");

        var realm = httpContext.GetCurrentRealm();

        var parameters = httpContext.Request.Query.AsNameValueCollection();
        if (realm.Options.StoreAuthorizationParameters)
        {
            var messageStoreId = parameters[Oidc.Routes.Params.Authorization];
            if (messageStoreId is not null)
            {
                var authorizeParameters = storage.GetAuthorizeParametersStore(realm);
                parameters = await authorizeParameters.ReadAsync(messageStoreId, httpContext.RequestAborted);
                await authorizeParameters.DeleteAsync(messageStoreId, httpContext.RequestAborted);
            }
        }

        if (parameters is null)
            return EndpointErrorResults.BadRequest(
                httpContext,
                Oidc.Authorize.Errors.InvalidRequest,
                "Invalid parameters");

        var user = httpContext.User;

        if (user is null)
        {
            return EndpointErrorResults.BadRequest(httpContext, Oidc.Authorize.Errors.LoginRequired, "Login required");
        }

        var context = new AuthorizeContext(httpContext, parameters, user);

        context.Load(logger);

        // The consent screen appends this marker to the callback URL when the resource owner denies
        // consent. It is read from the raw query (not the stored authorize parameters) so it works
        // regardless of the realm's StoreAuthorizationParameters setting.
        context.UserDeniedConsent =
            httpContext.Request.Query[Oidc.Routes.Params.ConsentDenied] == "true";

        return new EndpointCreationResult(context);
    }
}
