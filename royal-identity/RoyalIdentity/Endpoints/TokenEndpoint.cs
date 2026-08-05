// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Endpoints;

public class TokenEndpoint : IEndpointHandler
{
    private readonly IExtensionsGrantsProvider extensionsGrantsProvider;
    private readonly ILogger logger;

    public TokenEndpoint(
        IExtensionsGrantsProvider extensionsGrantsProvider,
        ILogger<TokenEndpoint> logger)
    {
        this.extensionsGrantsProvider = extensionsGrantsProvider;
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing token request.");

        var realmOptions = httpContext.GetRealmOptions();

        // validate HTTP method
        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP request for token endpoint, invalid method");

            return EndpointErrors.MethodNotAllowed(httpContext, HttpMethods.Post);
        }

        // validate HTTP content type
        if (!httpContext.Request.HasApplicationFormContentType())
        {
            logger.LogWarning("Invalid HTTP request for token endpoint, content type");

            return EndpointErrors.UnsupportedMediaType(httpContext);
        }

        // read parameters
        var form = await httpContext.Request.ReadFormAsync();
        var parameters = form.AsNameValueCollection();

        // Cardinality and the client authentication mechanism are decided here, before any parameter is read as
        // a scalar and before any evaluator can consult a store or burn a replay handle (DF7/DF8).
        if (!DirectRequestPreflight.TryEvaluate(
                httpContext,
                parameters,
                DirectRequestPreflight.TokenRequestParameters,
                logger,
                out var clientAuthentication,
                out var preflightFailure))
        {
            return preflightFailure;
        }

        // validate request
        if (!parameters.TryGet(Oidc.Token.Request.GrantType, out var grantType))
        {
            logger.LogWarning("Grant type parameter not found");

            return EndpointErrorResults.BadRequest(
                httpContext,
                Oidc.Token.Errors.InvalidRequest,
                "Grant type parameter not found");
        }

        if (grantType.Length > realmOptions.InputLengthRestrictions.GrantType)
        {
            logger.LogError("Grant type is too long");

            return EndpointErrorResults.BadRequest(
                httpContext,
                Oidc.Token.Errors.InvalidRequest,
                "Grant type is too long");
        }

        // create the context
        var items = new ContextItems();
        ITokenEndpointContextBase? context = null;
        switch (grantType)
        {
            case OpenIdConnectGrantTypes.AuthorizationCode:
                context = new AuthorizationCodeContext(httpContext, parameters, items);
                break;
            case OpenIdConnectGrantTypes.RefreshToken:
                context = new RefreshTokenContext(httpContext, parameters, items);
                break;
            case OpenIdConnectGrantTypes.ClientCredentials:
                context = new ClientCredentialsContext(httpContext, parameters, items);
                break;
            default:
                if (extensionsGrantsProvider.GetAvailableGrantTypes().Contains(grantType))
                {
                    context = await extensionsGrantsProvider.CreateContextAsync(grantType, httpContext.RequestAborted);
                }
                break;
        }

        // validate if grant type is supported
        if (context is null)
        {
            logger.LogError("Grant type not supported: {GrantType}", grantType);

            return EndpointErrorResults.BadRequest(httpContext, Oidc.Token.Errors.UnsupportedGrantType, "Grant type not supported");
        }

        // Set after the switch so an extension grant, which builds its own context and items, is covered too.
        context.Items.Set(clientAuthentication);

        context.Load(logger);

        return new EndpointCreationResult(context);
    }
}
